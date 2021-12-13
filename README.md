
A fork of https://github.com/raspi/WinLLDPService

Small LLDP Service for windows or *nix.

Used by network switches to list network devices - https://en.wikipedia.org/wiki/Link_Layer_Discovery_Protocol

Sends LLDP packets to switch. Switch must have LLDP capability. It doesn't query switches' internal LLDP data. Errors are logged to Windows Event Log.

# Interesting resources

https://www.ntkernel.com/windows-packet-filter/ (https://github.com/wiresock/ndisapi)
https://nmap.org/npcap/
