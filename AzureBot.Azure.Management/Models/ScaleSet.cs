namespace AzureBot.Azure.Management.Models
{
    using System;

    [Serializable]
    public class ScaleSet
    {
        public string SubscriptionId { get; set; }

        public string ResourceGroup { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }
        public long Capacity { get; set; }
    }
}