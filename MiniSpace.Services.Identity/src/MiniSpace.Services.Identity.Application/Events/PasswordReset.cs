using System;
using Convey.CQRS.Events;

namespace MiniSpace.Services.Identity.Application.Events
{
    public class PasswordReset : IEvent
    {
        public Guid UserId { get; }

        public PasswordReset(Guid userId)
        {
            UserId = userId;
        }
    }
}
