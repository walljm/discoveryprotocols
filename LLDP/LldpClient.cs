using Microsoft.Extensions.Logging;
using PacketDotNet;
using PacketDotNet.Lldp;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace LLDP
{
    /// <summary>
    /// Base classes needed to
    /// - Get adapters
    /// - Construct packets
    /// - Send packets
    /// </summary>
    public class LldpClient
    {
        private static readonly PhysicalAddress LldpDestinationMacAddress = new(
            new byte[] { 0x01, 0x80, 0xc2, 0x00, 0x00, 0x0e }
        );

        private readonly ILogger<LldpClient> logger;
        private readonly object lockObject = new();

        public LldpClient(ILogger<LldpClient> logger)
        {
            this.logger = logger;
        }

        public bool Send(LldpOptions lldpOptions)
        {
            lock (lockObject) // only one at a time in case some one tries in in parallel
            {
                // Get list of connected adapters
                List<NetworkInterface> adapters = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(
                        x =>
                            // Link is up
                            x.OperationalStatus == OperationalStatus.Up

                            // Not loopback (127.0.0.1 / ::1)
                            && x.NetworkInterfaceType != NetworkInterfaceType.Loopback

                            // Not tunnel
                            && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel

                            // Supports IPv4 or IPv6
                            && (
                                    x.Supports(NetworkInterfaceComponent.IPv4) ||
                                    x.Supports(NetworkInterfaceComponent.IPv6)
                               )
                    )
                    .ToList();

                if (!adapters.Any())
                {
                    // No available adapters
                    return false;
                }

                // Capture devices
                CaptureDeviceList captureDevices = CaptureDeviceList.Instance;

                if (captureDevices.Count == 0)
                {
                    // No available devices
                    return false;
                }

                // Wait time in milliseconds
                int waitTime = 10000;
                Stopwatch sw = new();

                foreach (NetworkInterface adapter in adapters)
                {
                    sw.Reset();

                    if (captureDevices.FirstOrDefault(x => x.Name.ToLower().Contains(adapter.Id.ToLower())) is not LibPcapLiveDevice device)
                    {
                        // Device not found, skip
                        Debug.WriteLine("No capture devices found");
                        continue;
                    }

                    try
                    {
                        if (!device.Opened)
                        {
                            // Open device in promiscuous mode
                            device.Open(DeviceModes.Promiscuous, waitTime);
                        }

                        sw.Start();
                        do
                        {
                            Thread.Sleep(15);
                        }
                        while (!device.Opened || sw.ElapsedMilliseconds >= waitTime);

                        sw.Stop();

                        // Send packet
                        logger.LogInformation($"Sending packet to Interface Name: {adapter.Name}, Description: {adapter.Description}");
                        Packet packet = CreateLldpPacket(adapter, lldpOptions);
                        SendRawPacket(device, packet);
                    }
                    catch (PcapException e)
                    {
                        logger.LogError("Error sending LLDP packet", e);
                    }
                    finally
                    {
                        if (device.Opened)
                        {
                            // Close device
                            device.Close();
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Generate LLDP packet for adapter
        /// </summary>
        private Packet CreateLldpPacket(NetworkInterface adapter, LldpOptions lldpOptions)
        {
            PhysicalAddress macAddress = adapter.GetPhysicalAddress();

            IPInterfaceProperties ipProperties = adapter.GetIPProperties();
            IPv4InterfaceProperties ipv4Properties = null; // Ipv4
            IPv6InterfaceProperties ipv6Properties = null; // Ipv6
            var ifIndex = -1;

            // IPv6
            if (adapter.Supports(NetworkInterfaceComponent.IPv6))
            {
                try
                {
                    ipv6Properties = ipProperties.GetIPv6Properties();
                    ifIndex = ipv6Properties.Index;
                }
                catch (NetworkInformationException e)
                {
                    // Adapter doesn't probably have IPv6 enabled
                    logger.LogError(e.Message);
                }
            }

            // IPv4
            if (adapter.Supports(NetworkInterfaceComponent.IPv4))
            {
                try
                {
                    ipv4Properties = ipProperties.GetIPv4Properties();
                    ifIndex = ipv6Properties.Index;
                }
                catch (NetworkInformationException e)
                {
                    // Adapter doesn't probably have IPv4 enabled
                    logger.LogError(e.Message, e);
                }
            }

            // Capabilities enabled
            List<CapabilityOptions> capabilitiesEnabled = new()
            {
                CapabilityOptions.StationOnly,
            };

            // Capabilities enabled
            if (ipv4Properties != null)
            {
                if (ipv4Properties.IsForwardingEnabled)
                {
                    capabilitiesEnabled.Add(CapabilityOptions.Router);
                }
            }

            ushort systemCapabilities = GetCapabilityOptionsBits(GetCapabilityOptions(adapter));
            ushort systemCapabilitiesEnabled = GetCapabilityOptionsBits(capabilitiesEnabled);

            // Constuct LLDP packet
            LldpPacket lldpPacket = new();

            lldpPacket.TlvCollection.Add(new ChassisIdTlv(ChassisSubType.MacAddress, macAddress));
            lldpPacket.TlvCollection.Add(new PortIdTlv(PortSubType.LocallyAssigned, Encoding.UTF8.GetBytes(adapter.Id)));
            lldpPacket.TlvCollection.Add(new TimeToLiveTlv(120));
            lldpPacket.TlvCollection.Add(new PortDescriptionTlv(
                    $"Name: {adapter.Name}, Index: {ifIndex}, Desc: {adapter.Description}, Speed: {ReadableSize(adapter.Speed)}"
                ));
            lldpPacket.TlvCollection.Add(new SystemNameTlv(lldpOptions.SystemName));
            lldpPacket.TlvCollection.Add(new SystemDescriptionTlv(lldpOptions.SystemDescription));
            lldpPacket.TlvCollection.Add(new SystemCapabilitiesTlv(systemCapabilities, systemCapabilitiesEnabled));

            // Add management address(es)
            if (ifIndex != -1) // you've got IPv4 or IPv6
            {
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
                    lldpPacket.TlvCollection.Add(new ManagementAddressTlv(
                        new NetworkAddress(ip.Address),
                        InterfaceNumber.SystemPortNumber,
                        Convert.ToUInt32(ifIndex), string.Empty)
                    );
                }
            }

            // End of LLDP packet
            lldpPacket.TlvCollection.Add(new EndOfLldpduTlv());

            if (lldpPacket.TlvCollection.Count == 0)
            {
                throw new ArgumentException("Couldn't construct LLDP TLVs.");
            }

            if (lldpPacket.TlvCollection.Last().GetType() != typeof(EndOfLldpduTlv))
            {
                throw new ArgumentException("Last TLV must be type of 'EndOfLLDPDU'!");
            }

            logger.LogDebug($"{lldpPacket.ToString(StringOutputType.VerboseColored)}");

            // Generate packet
            Packet packet = new EthernetPacket(macAddress, LldpDestinationMacAddress, EthernetType.Lldp)
            {
                PayloadData = lldpPacket.Bytes
            };

            return packet;
        }

        /// <summary>
        /// Change for example adapter link speed into more human readable format
        /// </summary>
        public static string ReadableSize(double size, int unit = 0)
        {
            string[] units = { "b", "K", "M", "G", "T", "P", "E", "Z", "Y" };

            while (size >= 1000)
            {
                size /= 1000;
                ++unit;
            }

            return string.Format("{0:G4}{1}", size, units[unit]);
        }

        /// <summary>
        /// Send packet using SharpPcap
        /// </summary>
        private void SendRawPacket(LibPcapLiveDevice device, Packet payload)
        {
            if (device == null)
            {
                logger.LogError($"{nameof(LibPcapLiveDevice)} was null. Unable to send packet");
                return;
            }

            if (device.Opened)
            {
                device.SendPacket(payload);
                return;
            }

            logger.LogError($"{nameof(LibPcapLiveDevice)} was not open. Unable to send packet");
            return;
        }

        /// <summary>
        /// List possible LLDP capabilities such as:
        /// - Bridge
        /// - Router
        /// - WLAN Access Point
        /// - Station
        /// </summary>
        private static List<CapabilityOptions> GetCapabilityOptions(NetworkInterface adapter)
        {
            List<CapabilityOptions> capabilities = new()
            {
                CapabilityOptions.Bridge,
                CapabilityOptions.Router,
                CapabilityOptions.StationOnly
            };

            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                capabilities.Add(CapabilityOptions.WLanAP);
            }

            return capabilities;
        }

        /// <summary>
        /// Get LLDP capabilities as bits
        /// </summary>
        private static ushort GetCapabilityOptionsBits(List<CapabilityOptions> capabilities)
        {
            ushort caps = 0;

            foreach (var cap in capabilities)
            {
                ushort tmp = ushort.Parse(cap.GetHashCode().ToString());
                caps |= tmp;
            }

            return caps;
        }
    }
}