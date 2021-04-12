namespace ITVComponents.Decisions
{
    /// <summary>
    /// Provides verification of data before it is imported
    /// </summary>
    /// <typeparam name="T">the data-type that is provided by the underlaying consumer</typeparam>
    public interface IConstraint<T>: IConstraint where T:class
    {
        /// <summary>
        /// Sets the Parent of this Constraint
        /// </summary>
        /// <param name="parent">the new Parent of this constraint</param>
        void SetParent(IDecider<T> parent);

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        DecisionResult Verify(T data, out string message);
    }
}
