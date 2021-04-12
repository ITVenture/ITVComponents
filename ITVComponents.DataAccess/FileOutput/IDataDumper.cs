namespace ITVComponents.DataAccess.FileOutput
{
    public interface IDataDumper
    {
        /// <summary>
        /// Gets the Path that will contain all dumped data
        /// </summary>
        string DumperPath { get; }

        /// <summary>
        /// Tries to load content from an xml file. If the file does not exists, an empty array is returned
        /// </summary>
        /// <param name="fileName">the filename from which to load data</param>
        /// <returns>the list of retreived objects or an empty array if the file does not exist</returns>
        DynamicResult[] TryLoadData(string fileName);

        /// <summary>
        /// Dumps an array of database retreived items into an xml
        /// </summary>
        /// <param name="items">the list of items to be dumped into an xml</param>
        /// <param name="fileName">the filename within the dumpdirectory in which to save the dumpfile</param>
        void DumpData(DynamicResult[] items, string fileName);

        /// <summary>
        /// Reads the content of an xml file and creates dynamic results of it
        /// </summary>
        /// <param name="fileName">the filename in which a datatable is stored</param>
        /// <returns>a list of items found in the xml file</returns>
        DynamicResult[] LoadData(string fileName);
    }
}