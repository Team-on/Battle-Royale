﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Common;
using Network;


namespace Server {
	class TCPServer : Common.IServer {
		TcpListener server;
		bool isRunning;
		Thread serverThread;
		object clientsLocker = new object();
		List<ClientInfo> clients;
		ConcurrentQueue<BasePlayerAction> playerActions;

		IPAddress ip;
		ushort port;

		public event Func<ClientConnect, ClientConnectResponce> ClientConnected;

		public TCPServer() {
			clients = new List<ClientInfo>();
			playerActions = new ConcurrentQueue<BasePlayerAction>();
		}

		public void StartServer(string _ip, ushort _port) {
			this.ip = IPAddress.Parse(_ip);
			this.port = _port;
			isRunning = true;

			server = new TcpListener(ip, port);
			serverThread = new Thread(() => {
				ProcessServer();
			});

			server.Start();
			serverThread.Start();
		}

		public void StopServer() {
			isRunning = false;
			while (serverThread.IsAlive)
				Thread.Sleep(250);
			server.Stop();
		}

		public void KickAllPlayers() {
			lock (clientsLocker) {
				foreach (var client in clients) {
					if (client.isRunning)
						client.isRunning = false;
				}
			}
		}

		public bool TryDequeuePlayerAction(out BasePlayerAction playerAction) {
			return playerActions.TryDequeue(out playerAction);
		}

		void ProcessServer() {
			while (isRunning) {
				TcpClient client = server.AcceptTcpClient();
				Thread clientThread = new Thread(new ParameterizedThreadStart(ProcessClient));
				clientThread.Start(client);
			}
		}

		void ProcessClient(object _client) {
			if (!(_client is TcpClient))
				return;

			ClientInfo clientInfo = new ClientInfo {
				isRunning = true,
				thread = Thread.CurrentThread,
				client = _client as TcpClient
			};
			clientInfo.stream = clientInfo.client.GetStream();

			lock (clientsLocker) {
				clients.Add(clientInfo);
			}

			byte[] data = new byte[ClientConnect.OneObjectSize];

			{
				ClientConnect clientConnect;
				ClientConnectResponce responce;

				lock (clientInfo.locker) {
					while (!clientInfo.stream.DataAvailable)
						Thread.Sleep(1);

					PacketType type = Protocol.BaseRecieve(clientInfo.stream, out data);
					if (type == PacketType.ClientConnect) {
						clientConnect = ClientConnect.Deserialize(data);

						responce = ClientConnected(clientConnect);
						clientInfo.playerId = responce.playerId;
						clientInfo.Send(PacketType.ClientConnectResponce, ClientConnectResponce.Serialize(responce));
					}
					else
						throw new Exception("Recieve smth wrong in Server.ProcessClient()");
				}
			}

			data = new byte[BasePlayerAction.OneObjectSize];
			BasePlayerAction action;

			while (clientInfo.isRunning) {
				lock (clientInfo.locker) {
					if (!clientInfo.stream.DataAvailable)
						continue;

					PacketType type = Protocol.BaseRecieve(clientInfo.stream, out data);
					if (type == PacketType.PlayerAction) {
						action = BasePlayerAction.Deserialize(data);
						action.playerId = clientInfo.playerId;
						playerActions.Enqueue(action);
					}
				}
			}

			lock (clientInfo.locker) {
				clientInfo.stream.Close();
				clientInfo.client.Close();
			}
		}

		public void SendWorldState(GameObjectState[] worldState) {
			List<byte> data = new List<byte>();
			foreach (var state in worldState)
				data.AddRange(GameObjectState.Serialize(state));

			lock (clientsLocker) {
				foreach (var c in clients) {
					lock (c.locker)
						c.Send(PacketType.WorldState, data.ToArray());
				}
			}
		}
	}
}
