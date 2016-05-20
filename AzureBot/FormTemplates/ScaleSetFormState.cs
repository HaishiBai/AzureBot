namespace AzureBot.FormTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Management.Models;

    [Serializable]
    public class ScaleSetFormState
    {
        public ScaleSetFormState(IEnumerable<ScaleSet> availableScaleSets, Operations operation)
        {
            this.AvailableScaleSets = availableScaleSets;
            this.Operation = operation;
        }

        public string ScaleSet { get; set; }

        public IEnumerable<ScaleSet> AvailableScaleSets { get; private set; }

        public Operations Operation { get; private set; }

        public ScaleSet SelectedScaleSet
        {
            get
            {
                return this.AvailableScaleSets.Where(p => p.Name == this.ScaleSet).SingleOrDefault();
            }
        }
    }
}