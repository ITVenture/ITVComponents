using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Settings
{
    public class XmlSettings
    {
        private static XmlSettings defaultInstance;

        private string configName;

        private XmlDocument doc;

        private XmlElement configElement;

        private XmlElement sectionsElement;

        private XmlElement settingsElement;

        static XmlSettings()
        {
            defaultInstance = new XmlSettings();
            defaultInstance.Read();
        }

        private XmlSettings()
        {
            configName = Assembly.GetEntryAssembly().Location + ".xmlConfig";
        }

        public static XmlSettings Default => defaultInstance;

        public static void Initialize(string settingsLocation)
        {
            defaultInstance.configName = settingsLocation;
            defaultInstance.Read();
        }

        /// <summary>
        /// ReLoads the Xml defnitions from the configfile
        /// </summary>
        public void Read()
        {
            doc = new XmlDocument();
            if (File.Exists(configName))
            {
                doc.Load(configName);
                var elems = doc.ChildNodes.Cast<XmlElement>();
                configElement = elems.First(n => n.Name == "Configuration");
                var children = configElement.ChildNodes.Cast<XmlElement>().ToArray();
                sectionsElement = children.First(n => n.Name == "Sections");
                settingsElement = children.First(n => n.Name == "Settings");
            }
            else
            {
                doc.CreateXmlDeclaration("1.0", System.Text.Encoding.UTF8.EncodingName, "yes");
                configElement = doc.CreateElement("Configuration");
                sectionsElement = doc.CreateElement("Sections");
                settingsElement = doc.CreateElement("Settings");
                doc.AppendChild(configElement);
                configElement.AppendChild(sectionsElement);
                configElement.AppendChild(settingsElement);
            }
        }

        public void Save()
        {
            doc.Save(configName);
        }

        internal T GetSetting<T>(string name, Type explicitType)
        {
            T retVal = default(T);
            XmlElement tmp = sectionsElement.ChildNodes.Cast<XmlElement>().FirstOrDefault(n =>
                n.Attributes["Type"]?.Value == explicitType.FullName && n.Attributes["Name"]?.Value == name);
            if (tmp != null)
            {
                string sectionGuid = tmp.Attributes["Guid"].Value;
                XmlElement sectionConfig = settingsElement.ChildNodes.Cast<XmlElement>()
                    .First(n => n.Attributes["Guid"]?.Value == sectionGuid);
                retVal = TryReadSectionXml<T>(sectionConfig, explicitType);
            }

            return retVal;
        }

        internal void WriteSetting<T>(T value, string name, Type explicitType)
        {
            T retVal = default(T);
            XmlElement tmp = sectionsElement.ChildNodes.Cast<XmlElement>().FirstOrDefault(n =>
                n.Attributes["Type"]?.Value == explicitType.FullName && n.Attributes["Name"]?.Value == name);
            if (tmp == null)
            {
                string guid = Guid.NewGuid().ToString();
                XmlElement elem = doc.CreateElement("Section");
                elem.SetAttribute("Name", name);
                elem.SetAttribute("Type", explicitType.FullName);
                elem.SetAttribute("Guid", guid);
                sectionsElement.AppendChild(elem);
                elem = doc.CreateElement("Setting");
                elem.SetAttribute("Guid", guid);
                settingsElement.AppendChild(elem);
                tmp = elem;
            }
            else
            {
                tmp = settingsElement.ChildNodes.Cast<XmlElement>()
                    .First(n => n.Attributes["Guid"].Value == tmp.Attributes["Guid"].Value);
            }

            TrySerializeSectionXml(tmp, value, explicitType);
        }

        private void TrySerializeSectionXml<T>(XmlElement target, T value, Type explicitType)
        {
            var emptyNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
            var serializer = new XmlSerializer(explicitType);
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            using (var stream = new StringWriter())
            using (var writer = XmlWriter.Create(stream, settings))
            {
                serializer.Serialize(writer, value, emptyNamespaces);
                target.InnerXml = stream.ToString();
            }
        }

        private T TryReadSectionXml<T>(XmlElement source, Type explicitType)
        {
            XmlSerializer serializer = new XmlSerializer(explicitType);
            T retVal = default(T);
            try
            {
                StringReader str = new StringReader(source.InnerXml);
                retVal = (T) serializer.Deserialize(str);
            }
            catch (Exception x)
            {
                LogEnvironment.LogEvent(x.OutlineException(), LogSeverity.Error);
            }

            return retVal;
        }
    }
}
