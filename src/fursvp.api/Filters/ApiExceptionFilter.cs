﻿// <copyright file="ApiExceptionFilter.cs" company="skippyfox">
// Copyright (c) skippyfox. All rights reserved.
// Licensed under the MIT license. See the license.md file in the project root for full license information.
// </copyright>

namespace Fursvp.Api.Filters
{
    using Fursvp.Communication;
    using Fursvp.Domain.Authorization;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Logging;
    using System;

    /// <summary>
    /// Intercepts http and https calls when an exception is thrown.
    /// </summary>
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiExceptionFilter"/> class.
        /// </summary>
        /// <param name="logger">The application event logger.</param>
        /// <param name="emailer">The sender or suppressor of emails.</param>
        /// <param name="userAccessor">Accesses the current user info.</param>
        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IEmailer emailer, IUserAccessor userAccessor)
        {
            this.Logger = logger;
            this.Emailer = emailer;
            this.UserAccessor = userAccessor;
        }

        private ILogger<ApiExceptionFilter> Logger { get; }

        private IEmailer Emailer { get; }

        private IUserAccessor UserAccessor { get; }

        /// <summary>
        /// Handle an Exception caught by MVC. Called by MVC when an otherwise uncaught exception is thrown.
        /// </summary>
        /// <param name="context">The Exception context.</param>
        public override void OnException(ExceptionContext context)
        {
            if (context.Exception is NotAuthorizedException authEx)
            {
                this.OnException(context, StatusCodes.Status401Unauthorized, authEx.GetType().Name, authEx.Message, authEx.Type.Name);

                this.Logger?.LogInformation(authEx, authEx.Message);
            }
            else if (context.Exception is ValidationException validationEx)
            {
                this.OnException(context, StatusCodes.Status400BadRequest, validationEx.GetType().Name, validationEx.Message);

                this.Logger?.LogInformation(validationEx, validationEx.Message);
            }
            else
            {
                var ex = context.Exception;
                this.OnException(context, StatusCodes.Status500InternalServerError, ex.GetType().Name, "An internal server error occurred. Sorry about that. The error has been logged.");
                this.Logger?.LogError(ex, ex.Message);

                // TODO - put all of these values in config
                this.Emailer?.Send(new Email
                {
                    From = new EmailAddress { Address = "noreply@fursvp.com", Name = "Fursvp.com" },
                    To = new EmailAddress { Address = "where.is.skippy@gmail.com" },
                    Subject = "Error on FURsvp.com",
                    PlainTextContent = @$"FURsvp error.

Time: {DateTime.Now}
Method: {context?.HttpContext?.Request?.Method}
QueryString: {context?.HttpContext?.Request?.QueryString}
Path: {context?.HttpContext?.Request?.Path}
Client IP: {context?.HttpContext?.Connection?.RemoteIpAddress}
User: {this.UserAccessor?.User?.EmailAddress}
Trace Identifier: {context?.HttpContext?.TraceIdentifier}

{ex.ToString()}

Inner Exception: {(ex.InnerException == null ? "null" : ex.InnerException.ToString())}",
                });
            }

            base.OnException(context);
        }

        private void OnException(ExceptionContext context, int statusCode, string exception, string errorMessage, string entity = null)
        {
            context.HttpContext.Response.StatusCode = statusCode;
            context.Result = new JsonResult(new { exception, errorMessage, entity });
        }
    }
}
