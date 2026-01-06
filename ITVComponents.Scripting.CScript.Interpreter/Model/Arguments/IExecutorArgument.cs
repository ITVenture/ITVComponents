using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators =false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(AssignArguments), "AssignArguments")]
    [JsonDerivedType(typeof(BlockArguments), "BlockArguments")]
    [JsonDerivedType(typeof(CompareArguments), "CompareArguments")]
    [JsonDerivedType(typeof(ConditionalValueArguments), "ConditionalValueArguments")]
    [JsonDerivedType(typeof(ExecutionSwitchArguments), "ExecutionSwitchArguments")]
    [JsonDerivedType(typeof(FunctionArguments), "FunctionArguments")]
    [JsonDerivedType(typeof(IfBlockArguments), "IfBlockArguments")]
    [JsonDerivedType(typeof(IncrementArguments), "IncrementArguments")]
    [JsonDerivedType(typeof(IndexerArguments), "IndexerArguments")]
    [JsonDerivedType(typeof(LiteralArguments), "LiteralArguments")]
    [JsonDerivedType(typeof(LoopArguments), "LoopArguments")]
    [JsonDerivedType(typeof(MemberAccessArguments), "MemberAccessArguments")]
    [JsonDerivedType(typeof(LoopJumpArguments), "LoopJumpArguments")]
    [JsonDerivedType(typeof(NativeScriptArguments), "NativeScriptArguments")]
    [JsonDerivedType(typeof(NegateArguments), "NegateArguments")]
    [JsonDerivedType(typeof(NewArguments), "NewArguments")]
    [JsonDerivedType(typeof(OperationArguments), "OperationArguments")]
    [JsonDerivedType(typeof(ReturnArguments), "ReturnArguments")]
    [JsonDerivedType(typeof(SequenceArguments), "SequenceArguments")]
    [JsonDerivedType(typeof(SwitchArguments), "SwitchArguments")]
    [JsonDerivedType(typeof(SwitchCaseArguments), "SwitchCaseArguments")]
    [JsonDerivedType(typeof(ThrowArguments), "ThrowArguments")]
    [JsonDerivedType(typeof(TryArguments), "TryArguments")]
    [JsonDerivedType(typeof(TypeLiteralArguments), "TypeLiteralArguments")]
    [JsonDerivedType(typeof(UnaryOpArguments), "UnaryOpArguments")]
    [JsonDerivedType(typeof(ValueIsTypeArguments), "ValueIsTypeArguments")]
    public interface IExecutorArgument
    {
    }
}
