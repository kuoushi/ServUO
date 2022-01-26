/***************************************************************************
 *                     CustomRemoteAdminPacketHandlers.cs
 *                            -------------------
 *   begin                : Jan 24, 2022
 *   copyright            : (C) Michael Rosiles
 *   email                : zindryr@gmail.com
 *   website              : http://antonyho.net/
 *
 *   Copyright (C) 2022 Michael Rosiles
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *   
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *   GNU General Public License for more details.
 *   
 *   You should have received a copy of the GNU General Public License
 *   along with this program. If not, see <http://www.gnu.org/licenses/>.
 ***************************************************************************/

using System;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using Server;
using Server.Misc;
using Server.Network;
using Server.Engines.Chat;

namespace Server.RemoteAdmin
{
	public static class CustomRemoteAdminPacketHandlers
	{
		// private static UdpClient Listener;
		public static void Configure()
		{
			RemoteAdminHandlers.Register(0x41, new OnPacketReceive(Save));
			RemoteAdminHandlers.Register(0x42, new OnPacketReceive(Shutdown));
			RemoteAdminHandlers.Register(0x46, new OnPacketReceive(WorldBroadcast));
			RemoteAdminHandlers.Register(0x48, new OnPacketReceive(KeepAlive));
			RemoteAdminHandlers.Register(0x49, new OnPacketReceive(ChannelSend));

			Channel.AddStaticChannel("Discord");
			ChatActionHandlers.Register(0x61, true, new OnChatAction(RelayToDiscord));

			UDPListener();
		}

		private static void UDPListener()
		{
			Task.Run(async () =>
			{
				using (var udpClient = new UdpClient(27030))
				{
					Utility.PushColor(ConsoleColor.Green);
					Console.WriteLine("RCON Listening: *.*.*.*:27030");
					Utility.PopColor();
					
					while(true)
					{
						var receivedResults = await udpClient.ReceiveAsync();
						
						ProcessPacket(receivedResults, udpClient);
					}
				}
			});
		}

		private static void ProcessPacket(UdpReceiveResult data, UdpClient client)
		{
			byte[] default_header = { 255, 255, 255, 255 };

			byte[] b = data.Buffer;
			string endByte = Encoding.ASCII.GetString(b).Substring(b.Length - 1, 1);
			byte[] header = b.Take(4).ToArray();
			
			if(!header.SequenceEqual(default_header))
			{
				Utility.PushColor(ConsoleColor.Red);
				Console.WriteLine("RCON: Received invalid header in packet.");
				Utility.PopColor();
				client.Send(new byte[] { 255 }, 1, data.RemoteEndPoint);
				return;
			}

			if(endByte != "\n")
			{
				Utility.PushColor(ConsoleColor.Red);
				Console.WriteLine("RCON: Received invalid closing byte in packet.");
				Utility.PopColor();
				client.Send(new byte[] { 255 }, 1, data.RemoteEndPoint);
				return;
			}

			byte[] x = b.Take(b.Length - 1).Skip(4).ToArray();

			string content = Encoding.ASCII.GetString(x);
			Console.WriteLine("RCON: {0} command received.", Encoding.ASCII.GetString(x));

			client.Send(data.Buffer, data.Buffer.Length, data.RemoteEndPoint);
		}

		private static void RelayToDiscord(ChatUser from, Channel channel, string param)
		{
			ChatActionHandlers.ChannelMessage(from, channel, param);
			byte[] data = Encoding.ASCII.GetBytes("UO\tm\t" + from.Username + "\t" + param);
			using (UdpClient c = new UdpClient(3896))
				c.Send(data, data.Length, "10.0.0.5", 3896);
		}

		private static void WorldBroadcast(NetState state, PacketReader pvSrc)
		{
			string message = pvSrc.ReadUTF8String();
			int hue = pvSrc.ReadInt16();
			bool ascii = pvSrc.ReadBoolean();

			World.Broadcast(hue, ascii, message);
		}

		private static void ChannelSend(NetState state, PacketReader pvSrc)
		{
			string channel_name = pvSrc.ReadUTF8String();
			string message = pvSrc.ReadUTF8String();
			int hue = pvSrc.ReadInt16();
			Channel channel = Channel.FindChannelByName(channel_name);

			foreach (ChatUser user in channel.Users)
			{
				Mobile player = user.Mobile;
				player.SendMessage(hue, message);
			}
		}

		private static void KeepAlive(NetState state, PacketReader pvSrc)
		{
			Server.Network.PacketHandlers.TripTime(state, pvSrc);
		}

		private static void Save(NetState state, PacketReader pvSrc)
		{
			AutoSave.Save();
		}

		private static void Shutdown(NetState state, PacketReader pvSrc)
		{
			bool restart = pvSrc.ReadBoolean();
			bool save = pvSrc.ReadBoolean();

			Console.WriteLine("RemoteAdmin: shutting down server (Restart: {0}) (Save: {1}) [{2}]", restart, save, DateTime.Now);

			if (save && !AutoRestart.Restarting)
				AutoSave.Save();

			Core.Kill(restart);
		}
	}
}