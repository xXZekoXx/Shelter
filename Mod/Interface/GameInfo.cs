﻿using System;
using System.Text;
using UnityEngine;

namespace Mod.Interface
{
    public class GameInfo : Gui
    {
        private GUIStyle _text;
        private float _maxSpeed;
        private float _deltaTime;
        private float _fps;

        protected override void OnShow()
        {
            _text = new GUIStyle
            {
                alignment = TextAnchor.UpperRight,
                fontSize = 12
            };
            
            windowRect = new Rect(0, 0, Screen.width, Screen.height);
        }

        protected override void Render()
        {
            if (!PhotonNetwork.inRoom)
                return;

            // Multiplayer Ping: 99
            // The barbies lover (1/5)
            
            // Singleplayer Map: The City I
            // Speed
            
            
            
            StringBuilder output = new StringBuilder();

            output.Append("<color=#03FF5A>");
            
            output.Append(IN_GAME_MAIN_CAMERA.GameType);
            if (IN_GAME_MAIN_CAMERA.GameType == GameType.Multiplayer)
                output.AppendFormat(" Ping: {0}", PhotonNetwork.GetPing());
            output.Append("\n");

            output.AppendFormat("Map: {0}", FengGameManagerMKII.Level);
            
            output.Append("\n");
            if (IN_GAME_MAIN_CAMERA.GameType == GameType.Multiplayer)
            {
                var room = PhotonNetwork.Room;
                output.AppendFormat("{0} ({1}/{2})", room.Name.HexColor(), Room.CurrentPlayers, room.MaxPlayers);
            }
            else
            {
                var speed = Camera.main.GetComponent<IN_GAME_MAIN_CAMERA>().main_object.rigidbody.velocity.magnitude; //TODO: Show in new gui
                _maxSpeed = Mathf.Max(_maxSpeed, speed);
                output.AppendFormat("Speed: Current {0} Max {1}", speed, _maxSpeed);
            }
            output.Append("\n");

            output.AppendFormat("Titans: {0}", GameObject.FindGameObjectsWithTag("titan")?.Length ?? 0);
            
            output.Append("\n");

            output.AppendFormat("FPS: {0:0}", _fps);

            output.Append("</color>");
            
            GUI.Label(windowRect, output.ToString(), _text);
        }

        private void Update()
        {
            _deltaTime = (Time.deltaTime - _deltaTime) * 0.1f;
            _fps = Mathf.Floor(1f / _deltaTime);
        }
    }
}