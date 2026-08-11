using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

public partial class IrBuilder
{
    private const string UserMainName = "__novus_user_main";

    private void LowerResultReturningMain()
    {
        var userMain = _module.GetFunction("main");
        if (userMain == null ||
            !ResultUsagePolicy.TryGetTypes(userMain.ReturnType, out var okType, out var errorType))
        {
            return;
        }

        if (!ResultUsagePolicy.IsUnit(okType) || errorType == null)
        {
            _diagnostics.ReportError(
                ErrorCodes.InvalidMainResult,
                "main must return Result<(), E>",
                userMain.Location ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "")
            );
            return;
        }

        var errorTypeName = GetTypeName(errorType);
        var messageMethodName = _module.FindTraitMethod(errorTypeName, "message");
        var messageMethod = messageMethodName == null ? null : _module.GetFunction(messageMethodName);
        if (messageMethod == null || messageMethod.Parameters.Count == 0)
        {
            _diagnostics.ReportError(
                ErrorCodes.TraitNotImplemented,
                $"main error type '{errorTypeName}' must implement Error",
                userMain.Location ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "")
            );
            return;
        }

        var resultType = (IrEnumType)userMain.ReturnType;
        var errorVariant = resultType.Variants.First(variant => variant.Name == "Err");

        _module.RenameFunction(userMain, UserMainName);

        var wrapper = new IrFunction("main", IrIntType.I32, userMain.Visibility)
        {
            Location = userMain.Location
        };
        _module.AddFunction(wrapper);

        var resultName = "%main_result";
        var tagName = "%main_tag";
        var isErrorName = "%main_is_error";
        var errorName = "%main_error";
        var messageName = "%main_error_message";

        wrapper.LocalVariables.Add(new IrLocalVariable(resultName, resultType, false));
        wrapper.LocalVariables.Add(new IrLocalVariable(errorName, errorType, false));

        var entry = wrapper.CreateBasicBlock("entry");
        var ok = wrapper.CreateBasicBlock("main_ok");
        var error = wrapper.CreateBasicBlock("main_error");

        entry.AddInstruction(new IrCall(UserMainName, resultType, resultName));
        entry.AddInstruction(new IrExtractTag(tagName, new IrVariable(resultName, resultType)));
        entry.AddInstruction(new IrBinaryOp(
            isErrorName,
            IrBinaryOp.OpKind.Eq,
            new IrVariable(tagName, IrIntType.I32),
            new IrConstant(errorVariant.Tag, IrIntType.I32),
            IrBoolType.Instance));
        entry.AddInstruction(new IrConditionalBranch(
            new IrVariable(isErrorName, IrBoolType.Instance),
            error.Label,
            ok.Label));

        ok.AddInstruction(new IrReturn(new IrConstant(0, IrIntType.I32)));

        error.AddInstruction(new IrExtractVariantData(
            errorName,
            new IrVariable(resultName, resultType),
            "Err",
            0,
            errorType));

        var messageCall = new IrCall(messageMethodName!, messageMethod.ReturnType, messageName);
        messageCall.Arguments.Add(new IrBorrowValue(
            new IrVariable(errorName, errorType),
            messageMethod.Parameters[0].Type,
            false));
        error.AddInstruction(messageCall);

        var reportCall = new IrCall("__novus_program_failed", IrVoidType.Instance);
        reportCall.Arguments.Add(new IrVariable(messageName, messageMethod.ReturnType));
        error.AddInstruction(reportCall);
        error.AddInstruction(new IrReturn(new IrConstant(20, IrIntType.I32)));
    }
}
