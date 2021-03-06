﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Common;

using static System.Math;

namespace ServerLogic.Components {
	class MoveableWithAngle : BaseComponent {
		SolidBody solidBody;
		readonly byte speed;
		//bool isAbleToMove;

		public MoveableWithAngle(IGameObject owner, byte speed) : base(owner) {
			this.speed = speed;
			//isAbleToMove = true;
		}

		public override void ProcessMessage(IComponentMessage msg) {
			if (msg.ComponentMessageType == ComponentMessageType.MoveForward ||
				msg.ComponentMessageType == ComponentMessageType.MoveBackward ||
				msg.ComponentMessageType == ComponentMessageType.MoveLeft ||
				msg.ComponentMessageType == ComponentMessageType.MoveRight
			)
				ProcessMove(msg);
			//else if (msg.ComponentMessageType == ComponentMessageType.TickElapsed)
				//isAbleToMove = true;
		}

		void ProcessMove(IComponentMessage msg) {
			//if (isAbleToMove) {
				//isAbleToMove = false;
				double angle = solidBody.Angle;

				switch (msg.ComponentMessageType) {
					case ComponentMessageType.MoveBackward:
						angle += 180;
						break;
					case ComponentMessageType.MoveLeft:
						angle -= 90;
						break;
					case ComponentMessageType.MoveRight:
						angle += 90;
						break;
				}

				angle = angle * Math.PI / 180;
				int dX = (int)Math.Round(Cos(angle) * speed),
					   dY = (int)Math.Round(Sin(angle) * speed);
				solidBody.AddToCoords(dX, dY);
				Owner.IsUpdated = true;
			//}
		}

		public override bool CheckDependComponents() {
			solidBody = Owner.GetComponent<SolidBody>();

			return base.CheckDependComponents() && solidBody != null;
		}
	}
}
