using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Settings;

namespace ITVComponents.Formatting.PluginSystemExtensions
{
    public class PluginParameterFormatter: StringFormatProvider

    {
        /// <summary>
        /// indicates whether to include an object identifying the current environment in the format-prototype
        /// </summary>
        private bool includeEnvironment = false;

        /// <summary>
        /// Werte, die diesem Formatierer beim Erzeugen mitgegeben wurden. Sie haben das letzte Wort -
        /// siehe <see cref="FillDictionary"/>.
        /// </summary>
        private IDictionary<string,object> additionalArguments;

        /// <summary>
        /// Initializes a default instance of the PluginParameterFormatter class
        /// </summary>
        public PluginParameterFormatter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the PluginParameterFormatter class
        /// </summary>
        /// <param name="includeEnvironment">indicates whether to include the Environment object of the System-Namespace</param>
        public PluginParameterFormatter(bool includeEnvironment)
        {
            this.includeEnvironment = includeEnvironment;
        }

        public PluginParameterFormatter(bool includeEnvironment, IDictionary<string, object> additionalArguments):this(includeEnvironment)
        {
            this.additionalArguments = new Dictionary<string, object>(additionalArguments);
        }

        protected override string FormatStringInternal(string rawString, Dictionary<string, object> customStringFormatArguments)
        {
            return customStringFormatArguments.FormatText(rawString, TextFormat.DefaultFormatPolicyWithPrimitives);
        }

        /// <summary>
        /// Sammelt die Werte, die in den Ausdruecken zur Verfuegung stehen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Reihenfolge ist zugleich die Rangfolge: erst <c>$$Environment</c>, dann die Konstanten aus
        /// der Konfiguration, zuletzt die mitgegebenen Argumente - und die <b>ueberschreiben</b>, was schon
        /// dasteht. Wer einem Formatierer beim Erzeugen einen Wert mitgibt, meint genau diesen; der Aufruf
        /// ist die speziellere Angabe, die allgemeine Konfiguration die Vorbelegung.
        /// </para>
        /// <para>
        /// Die beiden anderen Quellen legen weiterhin mit <c>Add</c> ab: dort ist ein doppelter Name kein
        /// Vorrang, sondern ein Fehler in der Konfiguration - zwei Konstanten desselben Namens - und der
        /// soll auffallen, statt dass eine von beiden still gewinnt.
        /// </para>
        /// </remarks>
        protected override void FillDictionary(IDictionary<string, object> values)
        {
            if (includeEnvironment)
            {
                values.Add("$$Environment", typeof(Environment));
            }

            foreach (var item in from t in PluginConstSection.Helper.Parameters
                     select new { t.ConstIdentifier, t.ConstValue })
            {
                values.Add(item.ConstIdentifier, item.ConstValue);
            }

            if (additionalArguments != null)
            {
                foreach (var item in additionalArguments)
                {
                    values[item.Key] = item.Value;
                }
            }
        }
    }
}
