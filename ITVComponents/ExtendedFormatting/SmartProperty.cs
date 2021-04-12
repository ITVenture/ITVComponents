using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using ITVComponents.Annotations;

namespace ITVComponents.ExtendedFormatting
{
    /// <summary>
    /// Smart Property class that enables dynamic Value calculations of properties
    /// </summary>
    public class SmartProperty:INotifyPropertyChanged
    {
        private IBasicKeyValueProvider target;

        /// <summary>
        /// The target object that provides the demanded data
        /// </summary>
        public IBasicKeyValueProvider Target
        {
            get { return target; }
            set
            {
                INotifyPropertyChanged changed = target as INotifyPropertyChanged;
                if (changed != null)
                {
                    changed.PropertyChanged -= TargetChanged;
                }

                target = value;
                changed = target as INotifyPropertyChanged;
                if (changed != null)
                {
                    changed.PropertyChanged += TargetChanged;
                }
            }
        }

        /// <summary>
        /// the Getter method that is invoked to retreive the calculated value
        /// </summary>
        public SmartGetMethod GetterMethod { get; set; }

        /// <summary>
        /// The Setter method that is invoked to set the value of the calculated value
        /// </summary>
        public SmartSetMethod SetterMethod { get; set; }

        /// <summary>
        /// Gets or sets a set of monitored properties that must cause this property to notify a change
        /// </summary>
        public string[] MonitoredProperties { get; set; }

        /// <summary>
        /// Gets the calculated value of the smart property
        /// </summary>
        public dynamic Value
        {
            get { return GetterMethod(Target); }
            set
            {
                if (SetterMethod != null)
                {
                    SetterMethod(Target, value);
                    OnPropertyChanged("Value");
                }
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">the property that has changed</param>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Informs client objects about changes on this SmartProperty
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void TargetChanged(object sender, PropertyChangedEventArgs e)
        {
            if (MonitoredProperties != null &&
                MonitoredProperties.Any(n => n.Equals(e.PropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                OnPropertyChanged("Value");
            }
        }

        /// <summary>
        /// Tritt ein, wenn sich ein Eigenschaftswert ändert.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public delegate dynamic SmartGetMethod(IBasicKeyValueProvider target);

    public delegate dynamic SmartSetMethod(IBasicKeyValueProvider target, object value);
}
