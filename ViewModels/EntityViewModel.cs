using System.ComponentModel;

namespace TeleList.ViewModels
{
    /// <summary>
    /// View model representing an entity in the DataGrid.
    /// Implements INotifyPropertyChanged for UI binding updates.
    /// </summary>
    public class EntityViewModel : INotifyPropertyChanged
    {
        private bool _isMarked;
        private bool _isLastUsed;

        public string EntityType { get; set; } = string.Empty;
        public string BaseType { get; set; } = string.Empty;
        public string LocationStr { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Distance { get; set; }
        public string CalcDistance { get; set; } = string.Empty;

        /// <summary>
        /// Whether this entity is marked as skipped (displayed in red).
        /// Notifies UI when changed.
        /// </summary>
        public bool IsMarked
        {
            get => _isMarked;
            set
            {
                _isMarked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarked)));
            }
        }

        /// <summary>
        /// Whether this entity was the last one used to update INI coordinates.
        /// Displayed with a distinct highlight color.
        /// </summary>
        public bool IsLastUsed
        {
            get => _isLastUsed;
            set
            {
                _isLastUsed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLastUsed)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Generates a unique key for this entity based on type and coordinates.
        /// Format: "EntityType|X,Y,Z" with coordinates rounded to 2 decimal places.
        /// Used for marking/tracking entities across refreshes.
        /// </summary>
        public string GetEntityKey()
        {
            return $"{EntityType}|{X:F2},{Y:F2},{Z:F2}";
        }
    }
}
