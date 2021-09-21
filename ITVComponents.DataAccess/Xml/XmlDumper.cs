using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ITVComponents.DataAccess.FileOutput;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;

namespace ITVComponents.DataAccess.Xml
{
    public class XmlDumper : IDataDumper
    {
        /// <summary>
        /// the dataDumpDirectory used to dump Data
        /// </summary>
        private string dataDumpDirectory;

        /// <summary>
        /// Initializes a new instance of the XmlDumper class
        /// </summary>
        /// <param name="dataDumpDirectory">the target directory for dumping data</param>
        public XmlDumper(string dataDumpDirectory)
        {
            this.dataDumpDirectory = dataDumpDirectory;
        }

        /// <summary>
        /// Gets the path that contains the dumped data
        /// </summary>
        public string DumperPath
        {
            get { return dataDumpDirectory; }
        }

            /// <summary>
        /// Tries to load content from an xml file. If the file does not exists, an empty array is returned
        /// </summary>
        /// <param name="fileName">the filename from which to load data</param>
        /// <returns>the list of retreived objects or an empty array if the file does not exist</returns>
        public DynamicResult[] TryLoadData(string fileName)
        {
            DynamicResult[] retVal = new DynamicResult[0];
            if (File.Exists(string.Format(@"{0}\{1}", dataDumpDirectory, fileName)))
            {
                retVal = LoadData(fileName);
            }

            return retVal;
        }

        /// <summary>
        /// Dumps an array of database retreived items into an xml
        /// </summary>
        /// <param name="items">the list of items to be dumped into an xml</param>
        /// <param name="fileName">the filename within the dumpdirectory in which to save the dumpfile</param>
        public void DumpData(DynamicResult[] items, string fileName)
        {
            string completeFileName = string.Format(@"{0}\{1}", dataDumpDirectory, fileName);
            string fillFileName = $"{completeFileName }.new";
            string oldFileName = $"{completeFileName}.old";
            using (var stream = new FileStream(fillFileName, FileMode.Create, FileAccess.Write))
            {
                DumpData(items, stream);
            }

            bool b = File.Exists(completeFileName);
            if (File.Exists(oldFileName))
            {
                File.Delete(oldFileName);
            }

            if (b)
            {
                File.Move(completeFileName, oldFileName);
            }

            File.Move(fillFileName, completeFileName);
            if (b)
            {
                File.Delete(oldFileName);
            }
        }

        /// <summary>
        /// Dumps an array of data items into the target stream
        /// </summary>
        /// <param name="items">the items to be dumped into an xml structure</param>
        /// <param name="target">the target stream to dump into</param>
        public static void DumpData(DynamicResult[] items, Stream target)
        {
            
            IEnumerable<string> fieldNames = null;
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.Default;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.Indent = true;
            settings.IndentChars = " ";
            settings.NewLineChars = "\r\n";
            settings.NewLineOnAttributes = false;
            using (XmlWriter wr = XmlWriter.Create(target, settings))
            {
                //LogEnvironment.LogEvent("so it begins...", LogSeverity.Report);
                wr.WriteStartDocument(true);
                wr.WriteStartElement("Table");
                try
                {
                    //LogEnvironment.LogEvent($"Record-count: {items.Length}", LogSeverity.Report);
                    foreach (DynamicResult dyn in items)
                    {

                        if (fieldNames == null)
                        {
                            fieldNames = dyn.GetDynamicMemberNames();
                        }

                        wr.WriteStartElement("Record");
                        try
                        {
                            foreach (string s in fieldNames)
                            {
                                //LogEnvironment.LogEvent($"Column name: {s}", LogSeverity.Report);
                                wr.WriteStartElement("Column");
                                wr.WriteAttributeString("Name", s);
                                try
                                {
                                    object obj = dyn[s];
                                    if (obj is DBNull)
                                    {
                                        obj = null;
                                    }

                                    wr.WriteAttributeString("Type", (obj != null) ? obj.GetType().FullName : "NULL");
                                    if (!(obj is Guid))
                                    {
                                        wr.WriteValue(obj ?? string.Empty);
                                    }
                                    else
                                    {
                                        /*LogEnvironment.LogEvent(
                                            $"Writing guid value... {obj}", LogSeverity.Report);*/
                                        wr.WriteValue(obj?.ToString());
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogEnvironment.LogEvent(ex.Message, LogSeverity.Error, "DataAccess");
                                }
                                finally
                                {
                                    wr.WriteEndElement();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogEvent(ex.Message, LogSeverity.Error, "DataAccess");
                        }
                        finally
                        {
                            wr.WriteEndElement();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.Message, LogSeverity.Error, "DataAccess");
                }
                finally
                {
                    wr.WriteEndElement();
                }
            }
        }

        /// <summary>
        /// Reads the content of an xml file and creates dynamic results of it
        /// </summary>
        /// <param name="fileName">the filename in which a datatable is stored</param>
        /// <returns>a list of items found in the xml file</returns>
        public DynamicResult[] LoadData(string fileName)
        {
            string inputFile = string.Format(@"{0}\{1}", dataDumpDirectory, fileName);
            using (var stream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            {
                return LoadData(stream);
            }
        }

        /// <summary>
        /// Reads data that was dumped into an xml previously
        /// </summary>
        /// <param name="input">the input stream providing the data</param>
        /// <returns>an array of dynamic result</returns>
        public static DynamicResult[] LoadData(Stream input)
        {
            List<DynamicResult> retVal = new List<DynamicResult>();
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.CloseInput = true;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;
            Dictionary<string, object> data = new Dictionary<string, object>();
            using (XmlReader rd = XmlReader.Create(input, settings))
            {
                while (rd.Read())
                {
                    if (rd.NodeType == XmlNodeType.Element && rd.Name == "Record")
                    {
                        data.Clear();
                    }
                    else if (rd.NodeType == XmlNodeType.EndElement && rd.Name == "Record")
                    {
                        retVal.Add(new DynamicResult(data));
                    }
                    else if (rd.NodeType == XmlNodeType.Element && rd.Name == "Column")
                    {
                        string columnName = rd.GetAttribute("Name");
                        string typeName = rd.GetAttribute("Type");
                        if (typeName != "NULL")
                        {
                            if (!rd.IsEmptyElement)
                            {
                                while (rd.NodeType != XmlNodeType.Text && rd.NodeType != XmlNodeType.EndElement)
                                {
                                    rd.Read();
                                }

                                string tx = rd.Value;
                                Type targetType = Type.GetType(typeName);
                                if (targetType != typeof(Guid))
                                {
                                    data.Add(columnName, TypeConverter.Convert(tx,targetType));
                                }
                                else
                                {
                                    data.Add(columnName, Guid.Parse(tx));
                                }
                            }
                        }
                        else
                        {
                            data.Add(columnName, DBNull.Value);
                        }
                    }
                }
            }

            return retVal.ToArray();
        }
    }
}
