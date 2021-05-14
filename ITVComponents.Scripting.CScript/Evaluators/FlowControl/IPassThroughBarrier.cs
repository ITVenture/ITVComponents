namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public interface IPassThroughBarrier
    {
        /// <summary>
        /// Gets a value indicating whether the current PassThroughBarrier instance is able to handle the provided PassThrough value
        /// </summary>
        /// <param name="ptv">the value to check, whether it can be handled by this barrier</param>
        /// <returns>a value indicating whether the passThrough stops here</returns>
        bool CanHandle(PassThroughValue ptv);
    }
}
