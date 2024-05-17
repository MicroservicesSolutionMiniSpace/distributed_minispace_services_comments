﻿using Convey.CQRS.Events;
using Convey.MessageBrokers;

namespace MiniSpace.Services.Students.Application.Events.External
{
    [Message("events")]
    public class StudentCancelledInterestInEvent : IEvent
    {
        public Guid EventId { get; }
        public Guid StudentId { get; }

        public StudentCancelledInterestInEvent(Guid eventId, Guid studentId)
        {
            EventId = eventId;
            StudentId = studentId;
        }
    } 
}