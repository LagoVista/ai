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
                    Description = "Tools and data structures for configuring and managing AI models, agents, and their conversations.",
                    DomainType = DomainDescription.DomainTypes.BusinessObject,
                    Name = "AI Administration",
                    CurrentVersion = new Core.Models.VersionInfo()
                    {
                        Major = 2,
                        Minor = 0,
                        Build = 001,
                        DateStamp = new DateTime(2026, 1, 31),
                        Revision = 1,
                        ReleaseNotes = "Initial unstable preview"
                    },
                    Clusters = new List<Cluster>()
                };
            }
        }
    }
}