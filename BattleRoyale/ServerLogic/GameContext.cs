﻿using System;
using System.Collections.Generic;

using Common;
using ServerLogic.Components;
using ServerLogic.ComponentMessage;
using ServerLogic.GameObject;

namespace ServerLogic {
	class GameContext {
		#region Singletone
		static GameContext gameContext;

		static public GameContext GetGCState() {
			return gameContext;
		}

		static GameContext() {
			gameContext = new GameContext();
		}

		private GameContext() {
			map = new List<BaseMapObject>();
			players = new List<PlayerObject>();
			gameObjects = new List<BaseGameObject>();
			toRemove = new List<BaseGameObject>();
			isRunning = true;
		}
		#endregion

		IServer server;
		List<BaseMapObject> map;
		List<PlayerObject> players;
		List<BaseGameObject> gameObjects;
		List<BaseGameObject> toRemove;

		bool isRunning;

		public void SetServer(IServer server) {
			this.server = server;

			server.ClientConnected += (a) => {
				PlayerObject player = new PlayerObject(new Coord(113, 113), a.playerChampionType);
				players.Add(player);

				return new ClientConnectResponce() {
					playerId = player.Id,
					initialWorldState = GetAllTexturedStates(),
				};
			};
		}

		public void LoadMap() {
			map.Clear();
			players.Clear();
			gameObjects.Clear();

			for (byte i = 0; i < 50; ++i) {
				for (byte j = 0; j < 50; ++j) {
					if (i == 0 || j == 0 || i == 49 || j == 49)
						map.Add(new WallMapObject(new Coord((uint)(i * 50), (uint)(j * 50)), new Size(51, 51), TextureId.Water));
					else
						map.Add(new FloorMapObject(new Coord((uint)(i * 50), (uint)(j * 50)), new Size(51, 51), TextureId.Grass));
				}
			}

		}

		public void StartGame() {
			ProcessGame();
		}

		public void StopGame() {
			isRunning = false;
		}

		public void ProcessGame() {
			//const int fps = 30;
			//const int skipTick = 1000 / fps;
			//int nextTick = Environment.TickCount;
			//int sleepTime = 0;

			//while (isRunning) {
			//	Update();
			//	Display();

			//	nextTick += skipTick;
			//	sleepTime = nextTick - Environment.TickCount;
			//	if (sleepTime >= 0) {
			//		System.Threading.Thread.Sleep(sleepTime);
			//	}
			//	else {
			//		// Shit, we are running behind!
			//	}
			//}

			//Calculations per second (like fps)
			const int cps = 24;
			const int maxFps = 24;
			const int skipTickcps = 1000 / cps;
			const int skipTickfps = 1000 / maxFps;
			const int maxFrameSkip = 10;

			int nextTickcps = Environment.TickCount;
			int nextTickfps = Environment.TickCount;
			int loops;

			while (isRunning) {
				loops = 0;
				while (Environment.TickCount > nextTickcps && loops < maxFrameSkip) {
					Update();

					nextTickcps += skipTickcps;
					loops++;
				}

				if (Environment.TickCount > nextTickfps) {
					Display();
					nextTickfps += skipTickfps;
				}
				else
					System.Threading.Thread.Sleep(1);

			}
		}

		public void AddGameObject(BaseGameObject obj) => gameObjects.Add(obj);

		void Display() {
			List<GameObjectState> states = new List<GameObjectState>(map.Count + players.Count + gameObjects.Count);
			Components.TexturedBody texturedObj;

			foreach (var i in map) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null && i.IsUpdated) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
					i.IsUpdated = false;
				}
			}

			foreach (var i in players) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null && i.IsUpdated) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
					i.IsUpdated = false;
				}
			}

			foreach (var i in gameObjects) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null && i.IsUpdated) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
					i.IsUpdated = false;
				}
			}

			server.SendChangedWorldState(states.ToArray());
		}

		const int disposeTime = 60000;
		int nextDisposeTime = Environment.TickCount + disposeTime;
		void Update() {
			ReadPlayersInput();
			ProcessMessages();

			if (Environment.TickCount > nextDisposeTime) {
				RemoveDisposedObjects();
				nextDisposeTime += disposeTime;
			}
		}

		public GameObjectState[] GetAllTexturedStates() {
			List<GameObjectState> states = new List<GameObjectState>(map.Count + players.Count + gameObjects.Count);
			Components.TexturedBody texturedObj;

			foreach (var i in map) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
				}
			}

			foreach (var i in players) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
				}
			}

			foreach (var i in gameObjects) {
				texturedObj = i.GetComponent<Components.TexturedBody>();
				if (texturedObj != null) {
					states.Add(new GameObjectState(
						texturedObj.TextureId, i.Id,
						texturedObj.Pos, texturedObj.Angle,
						texturedObj.Size
					));
				}
			}

			return states.ToArray();
		}

		void ReadPlayersInput() {
			ComponentMessageBase message;
			while (server.TryDequeuePlayerAction(out BasePlayerAction action)) {
				if (
					action.actionType == PlayerActionType.PlayerChangeAngle ||
					action.actionType == PlayerActionType.SkillLMB ||
					action.actionType == PlayerActionType.SkillRMB
				)
					message = new ComponentMessageAngle((ComponentMessageType)action.actionType, action.newAngle);
				else
					message = new ComponentMessageBase((ComponentMessageType)action.actionType);

				players.Find((p) => p.Id == action.playerId)?.
					SendMessage(message);
			}
		}

		void ProcessMessages() {
			foreach (var i in gameObjects)
				i.Process();
			foreach (var i in players)
				i.Process();
			//foreach (var i in map) 
			//	i.Process();

			ProcessCollide();
		}

		void ProcessCollide() {
			SolidBody solidObject;
			SolidBody cellSolid, playerSolid;

			foreach (var obj in gameObjects) {
				solidObject = obj.GetComponent<SolidBody>();
				if (solidObject == null && obj.IsDisposed())
					continue;

				foreach (var pl in players) {
					if (pl.IsDisposed())
						continue;

					playerSolid = pl.GetComponent<SolidBody>();
					if (playerSolid != null && solidObject.IsCollide(playerSolid)) {
						obj.SendMessage(new CollideMessage(pl));
					}
				}

				foreach (var cell in map) {
					if (cell.IsDisposed())
						continue;

					cellSolid = cell.GetComponent<SolidBody>();
					if (cellSolid != null && solidObject.IsCollide(cellSolid)) {
						obj.SendMessage(new CollideMessage(cell));
					}
				}
			}

			foreach (var pl in players) {
				playerSolid = pl.GetComponent<SolidBody>();
				if (playerSolid == null && pl.IsDisposed())
					continue;

				foreach (var cell in map) {
					if (cell.IsDisposed())
						continue;

					cellSolid = cell.GetComponent<SolidBody>();
					if (cellSolid != null && playerSolid.IsCollide(cellSolid)) {
						pl.SendMessage(new CollideMessage(cell));
					}
				}
			}
		}

		void RemoveDisposedObjects() {
			foreach (var i in gameObjects)
				if (i.IsDisposed())
					toRemove.Add(i);
			if (toRemove.Count == 0) {
				foreach (var i in toRemove)
					gameObjects.Remove(i);
				toRemove.Clear();
			}

			foreach (var i in players)
				if (i.IsDisposed())
					toRemove.Add(i);
			if (toRemove.Count == 0) {
				foreach (var i in toRemove)
					players.Remove(i as PlayerObject);
				toRemove.Clear();
			}

			//foreach (var i in map)
			//	if (i.IsDisposed())
			//		toRemove.Add(i);
			//if (toRemove.Count == 0) {
			//	foreach (var i in toRemove)
			//		map.Remove(i as BaseMapObject);
			//	toRemove.Clear();
			//}
		}
	}
}
