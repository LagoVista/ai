// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 0e45ef81908d3b9fcc955cb41f2656ddce093e371ab46b3a3418c6d7be92451f
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.Core.Attributes;
using LagoVista.Core.Models.UIMetaData;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Models
{
    [DomainDescriptor]
    public class AIDomain
    {
        public const string AIAdmin = "AI Admin";

        [DomainDescription(AIAdmin)]
        public static DomainDescription AIAdminDescription
        {
            get
            {
                return new DomainDescription()
                {
                    Description = "A set of classes that contains meta data for managing machine learning models.",
                    DomainType = DomainDescription.DomainTypes.BusinessObject,
                    Name = "AI Admin",
                    CurrentVersion = new Core.Models.VersionInfo()
                    {
                        Major = 0,
                        Minor = 8,
                        Build = 001,
                        DateStamp = new DateTime(2019, 11, 16),
                        Revision = 1,
                        ReleaseNotes = "Initial unstable preview"
                    }
                };
            }
        }
    }
}
