using System;
using System.Collections.Generic;

namespace LiverAR.Runtime
{
    [Serializable]
    public sealed class AnatomyInformationRecord
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string Overview;
        public string Location;
        public string BloodSupply;
        public string VenousDrainage;
        public string Function;
        public string Description;

        public string ToDisplayText()
        {
            return $"{DisplayName}\n\nOverview\n{Overview}\n\nAnatomical location\n{Location}\n\nBlood supply\n{BloodSupply}\n\nVenous drainage\n{VenousDrainage}\n\nFunction\n{Function}\n\nEducational notes\n{Description}";
        }
    }

    public static class AnatomyInformationCatalog
    {
        public static AnatomyInformationRecord Liver => new AnatomyInformationRecord
        {
            Id = "liver", DisplayName = "Liver", Category = "Liver",
            Overview = "The liver is the largest internal gland and a major organ of metabolism.",
            Location = "The upper-right abdomen, beneath the diaphragm and beside the stomach.",
            BloodSupply = "The hepatic artery and portal vein provide arterial and nutrient-rich inflow.",
            VenousDrainage = "Hepatic veins drain into the inferior vena cava.",
            Function = "Metabolism, bile production, nutrient storage, detoxification, and plasma protein synthesis.",
            Description = "This educational summary describes normal anatomy and is not medical advice."
        };

        public static AnatomyInformationRecord ForSegment(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Couinaud Segmentation",
                Overview = $"{name} is one of the functional liver segments in the Couinaud system.",
                Location = "Segment boundaries are defined by portal and hepatic venous anatomy; orientation may vary in the 3D model.",
                BloodSupply = "The segment receives portal inflow through its corresponding portal pedicle.",
                VenousDrainage = "Drainage follows nearby hepatic venous territories.",
                Function = "Segmental anatomy helps describe liver structure and supports educational orientation.",
                Description = "Use the 3D model and segmentation controls to compare this segment with neighbouring regions."
            };
        }

        public static AnatomyInformationRecord ForPart(AnatomyPart part)
        {
            if (part == null) return Liver;
            if (part.Category == AnatomyCategory.LiverSegment) return ForSegment(part.DisplayName);
            if (part.Category == AnatomyCategory.Vessel)
                return new AnatomyInformationRecord
                {
                    Id = part.StructureId, DisplayName = part.DisplayName, Category = "Blood Vessel",
                    Overview = "A vessel structure included in the patient model.", Location = "Shown in relation to the liver in the 3D scene.",
                    BloodSupply = "Vessel-specific inflow depends on the displayed structure.", VenousDrainage = "Vessel-specific drainage depends on the displayed structure.",
                    Function = "Supports blood flow through or away from the liver.", Description = "This educational model does not provide diagnosis or treatment advice."
                };
            return Liver;
        }

        public static readonly string[] SegmentNames = { "Segment I", "Segment II", "Segment III", "Segment IVa", "Segment IVb", "Segment V", "Segment VI", "Segment VII", "Segment VIII" };
        public static readonly string[] DiseaseNames = { "Fatty Liver", "Cirrhosis", "Alcohol-Related Liver Disease", "Liver Cancer", "Hepatitis" };

        public static AnatomyInformationRecord ForDisease(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Liver Disease",
                Overview = $"{name} is included as an educational topic in this application.",
                Location = "Changes may affect liver tissue and function.", BloodSupply = "Not applicable to this educational topic.",
                VenousDrainage = "Not applicable to this educational topic.", Function = "The effect on liver function varies by condition and individual.",
                Description = "Educational content only. This application does not provide diagnosis or treatment advice."
            };
        }

        public static AnatomyInformationRecord ForVessel(string name)
        {
            return new AnatomyInformationRecord
            {
                Id = name.ToLowerInvariant().Replace(" ", "-"), DisplayName = name, Category = "Blood Vessel",
                Overview = "A blood vessel structure shown in relation to the liver.",
                Location = "The exact course is represented by the currently loaded 3D model.",
                BloodSupply = "Vessel-specific inflow depends on the displayed structure.",
                VenousDrainage = "Vessel-specific drainage depends on the displayed structure.",
                Function = "Supports blood flow through or away from the liver.",
                Description = "This educational model does not provide diagnosis or treatment advice."
            };
        }
    }
}
