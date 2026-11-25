using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Telegram.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Activity.Telegram.Commands.ProcessTelegramUpdate;

internal sealed class ProcessTelegramUpdateCommandHandler(TelegramUpdateProcessor processor) : ICommandHandler<ProcessTelegramUpdateCommand>
{
    public async ValueTask<Result> Handle(ProcessTelegramUpdateCommand command, CancellationToken cancellationToken)
    {
        await processor.ProcessAsync(command.Update, cancellationToken);
        return Result.Success();
    }
}
