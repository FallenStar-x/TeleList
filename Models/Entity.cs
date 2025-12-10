using System;

namespace TeleList.Models
{
    /// <summary>
    /// Represents a game entity with type, location, and distance information.
    /// Parsed from entity data files by EntityParser.
    /// </summary>
    public class Entity
    {
        public string EntityType { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Distance { get; set; }

        public string LocationStr => $"{X:F2}, {Y:F2}, {Z:F2}";

        public string BaseType
        {
            get
            {
                // Extract the base type with subtype for better filtering.
                // Examples:
                // - 'Npc_p_npc_1234500102' -> 'Npc'
                // - 'InteractComEntity_sign_2_xxx' -> 'InteractComEntity_sign'
                // - 'InteractComEntity_ins_entity123_xxx' -> 'InteractComEntity_ins'
                // - 'Weapon_100065_xxx' -> 'Weapon'
                // - 'SimpleVisualEntity_p_npc_xxx' -> 'SimpleVisualEntity'

                var parts = EntityType.Split('_');
                if (parts.Length == 0)
                    return EntityType;

                var baseType = parts[0];

                // For certain entity types, include the subtype for better filtering
                if ((baseType == "InteractComEntity" || baseType == "SimpleVisualEntity" || baseType == "LocalEntity")
                    && parts.Length > 1)
                {
                    return $"{baseType}_{parts[1]}";
                }

                return baseType;
            }
        }

        public string GetEntityKey()
        {
            // Generate a unique key for an entity based on type and coordinates.
            // Format: entity_type|x,y,z (coordinates rounded to 2 decimal places)
            return $"{EntityType}|{X:F2},{Y:F2},{Z:F2}";
        }

        public static double CalculateDistance(Entity ref1, Entity target)
        {
            return Math.Sqrt(
                Math.Pow(ref1.X - target.X, 2) +
                Math.Pow(ref1.Y - target.Y, 2) +
                Math.Pow(ref1.Z - target.Z, 2)
            );
        }
    }
}
