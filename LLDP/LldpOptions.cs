using System;

namespace LLDP
{
    public class LldpOptions
    {
        public string SystemName { get; set; } = Environment.MachineName;
        public string SystemDescription { get; set; } = $"{Environment.OSVersion}, {Environment.ProcessorCount} Processors";
    }
}