using System;
using System.Collections.Concurrent;
using Expense.API.Models.Domain;
using Microsoft.AspNetCore.SignalR;

namespace Expense.API.Repositories.Notifications
{
	public class TextractNotificationHub: Hub
	{
		public TextractNotificationHub()
		{
		}
        // A concurrent dictionary to store user connections
        private static readonly ConcurrentDictionary<string, string> connections = new ConcurrentDictionary<string, string>();

        public override Task OnConnectedAsync()
        {
            string userName = Context.UserIdentifier; 

            connections.TryAdd(Context.ConnectionId, userName);

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            connections.TryRemove(Context.ConnectionId, out _);

            return base.OnDisconnectedAsync(exception);
        }
        public async Task SendTextractNotification(string userName, string message)
        {
            await Clients.User(userName).SendAsync("ReceiveMessage", message);
        }
    }
}

