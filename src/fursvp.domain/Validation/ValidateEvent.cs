﻿// <copyright file="ValidateEvent.cs" company="skippyfox">
// Copyright (c) skippyfox. All rights reserved.
// Licensed under the MIT license. See the license.md file in the project root for full license information.
// </copyright>

namespace Fursvp.Domain.Validation
{
    using System;
    using System.Linq;
    using Fursvp.Helpers;

    public class ValidateEvent : IValidate<Event>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateEvent"/> class.
        /// </summary>
        /// <param name="dateTimeProvider">An instance of <see cref="IProvideDateTime"/>.</param>
        /// <param name="validateMember">An instance of <see cref="IValidate{Member}"/> to provide Member state validation.</param>
        public ValidateEvent(IProvideDateTime dateTimeProvider, IValidate<Member> validateMember)
        {
            this.DateTimeProvider = dateTimeProvider;
            this.ValidateMember = validateMember;
            this.Assert = new Assertions<ValidationException<Event>>();
        }

        private IProvideDateTime DateTimeProvider { get; }

        private IValidate<Member> ValidateMember { get; }

        private Assertions<ValidationException<Event>> Assert { get; }

        public void ValidateState(Event oldState, Event newState)
        {
            if (oldState == null && newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            this.Assert.That(newState != null, "Deleting an Event is not allowed.");
            this.Assert.That(newState.Members?.Any() == true, "Members list cannot be empty or null.");
            this.Assert.That(newState.Members.Select(x => x.EmailAddress.ToLower()).Distinct().Count() == newState.Members.Count(), "Each member in list must have a unique email address.");
            this.Assert.That(newState.Members.Count(x => x.IsAuthor) == 1, "Members list must contain exactly one author of the event.");

            var oldMembers = oldState?.Members ?? Enumerable.Empty<Member>();
            var newMembers = newState?.Members ?? Enumerable.Empty<Member>();
            foreach (var memberState in oldMembers.FullJoin(newMembers, m => m.Id, m => m.Id, (old, @new) => new { old, @new }))
            {
                this.ValidateMember.ValidateState(memberState.old, memberState.@new);
            }

            this.Assert.That(newState.Form != null, "Form cannot be null.");
            if (!newState.RsvpOpen)
            {
                this.Assert.That(!newState.RsvpClosesAt.HasValue, "RsvpClosesAt cannot be set if Rsvp is closed.");
            }

            if (newState.RsvpOpen)
            {
                this.Assert.That(newState.RsvpClosesAt.HasValue, "RsvpClosesAt must be set if Rsvp is open.");
            }

            if (newState.IsPublished)
            {
                this.Assert.That(newState.StartsAt != default, "Event must have a start date and time.");
                this.Assert.That(newState.EndsAt != default, "Event must have an end date and time.");
                this.Assert.That(newState.StartsAt < newState.EndsAt, "Start time must be before End time.");

                if (oldState?.IsPublished != true)
                {
                    this.Assert.That(newState.StartsAt > this.DateTimeProvider.Now, "Start time must be in the future.");
                }

                this.Assert.That(newState.TimeZoneId != null, "Time Zone is required.");
            }
        }
    }
}
