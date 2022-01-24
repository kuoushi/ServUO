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
using Server;
using Server.Misc;
using Server.Network;
using Server.Engines.Chat;

namespace Server.RemoteAdmin
{
	public static class CustomRemoteAdminPacketHandlers
	{
		public static void Configure()
		{
			RemoteAdminHandlers.Register(0x41, new OnPacketReceive(Save));
			RemoteAdminHandlers.Register(0x42, new OnPacketReceive(Shutdown));
			RemoteAdminHandlers.Register(0x46, new OnPacketReceive(WorldBroadcast));
			RemoteAdminHandlers.Register(0x48, new OnPacketReceive(KeepAlive));
			RemoteAdminHandlers.Register(0x49, new OnPacketReceive(ChannelSend));

			Channel.AddStaticChannel("Discord");
			ChatActionHandlers.Register(0x61, true, new OnChatAction(RelayToDiscord));
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