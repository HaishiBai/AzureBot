﻿namespace AzureBot.Controllers
{
    using System;
    using System.Configuration;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Autofac;
    using Helpers;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Connector;

    public class OAuthCallbackController : ApiController
    {
        [HttpGet]
        [Route("api/OAuthCallback")]
        public async Task<HttpResponseMessage> OAuthCallback([FromUri] string code, [FromUri] string state)
        {
            // Get the resumption cookie
            var resumptionCookie = ResumptionCookie.GZipDeserialize(state);

            // Exchange the Auth code with Access token
            var token = await AzureActiveDirectoryHelper.GetTokenByAuthCodeAsync(code);

            // Create the message that is send to conversation to resume the login flow
            var msg = resumptionCookie.GetMessage();
            msg.Text = $"token:{token.AccessToken}&user:{token.UserInfo.GivenName} {token.UserInfo.FamilyName}";

            // Resume the conversation
            var reply = await Conversation.ResumeAsync(resumptionCookie, msg);

            // Remove the pending message because login flow is complete
            IBotData dataBag = new JObjectBotData(reply);
            ResumptionCookie pending;
            if (dataBag.PerUserInConversationData.TryGetValue(ContextConstants.PersistedCookieKey, out pending))
            {
                dataBag.PerUserInConversationData.RemoveValue(ContextConstants.PersistedCookieKey);

                using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, reply))
                {
                    // make sure that we have the right Channel info for the outgoing message
                    var persistedCookie = pending.GetMessage();
                    reply.To = persistedCookie.From;
                    reply.From = persistedCookie.To;

                    // Send the login success asynchronously to user
                    var client = scope.Resolve<IConnectorClient>();
                    await client.Messages.SendMessageAsync(reply);
                }

                return Request.CreateResponse(BotMessages.OAuthCallback_LoggedIn);
            }
            else
            {
                // Callback is called with no pending message as a result the login flow cannot be resumed.
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, new InvalidOperationException(BotMessages.OAuthCallback_CannotResume));
            }
        }
    }
}
