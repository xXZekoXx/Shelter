﻿using System;
using System.Collections.Generic;
using Mod.Managers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mod.Animation;
using Mod.Interface;
using UnityEngine;
using AnimationInfo = Mod.Animation.AnimationInfo;
using Animator = Mod.Animation.Animator;
using Random = UnityEngine.Random;

namespace Mod
{
    public class Shelter : MonoBehaviour
    {
        public static readonly string ModDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Shelter\\";
        public static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
        public static List<Profile> Profiles => _profileManager.ProfileFile.Profiles;
        public static Profile Profile => _profileManager.ProfileFile.SelectedProfile;
        private static AnimationInfo _animation;
        private static InterfaceManager _interfaceManager;
        private static CommandManager _commandManager;
        private static AnimationManager _animationManager;
        private static ProfileManager _profileManager;
//        private static DiscordRpc _discord;        

        public Shelter()
        {
            if (!Directory.Exists(ModDirectory))
                Directory.CreateDirectory(ModDirectory);
            gameObject.AddComponent<AnimationTest>();

            _animation = new AnimationInfo(AnimationType.Cycle, AnimationInfo.Rainbow);
        }
        
        public void InitComponents()
        {
            _interfaceManager = new InterfaceManager();
            _profileManager = new ProfileManager();
            _animationManager = new AnimationManager();
//            _discord = new DiscordRpc();;
        }

        public void Update()
        {
//            _discord.Update();
            if (Input.GetKeyDown(KeyCode.I) && Input.GetKey(KeyCode.LeftControl))
                File.WriteAllLines($"GameObjects{Random.Range(0, 255)}.txt", FindObjectsOfType(typeof(GameObject)).OrderBy(x => x.GetInstanceID()).Select(x => $"{x.GetInstanceID()} - {x.name}").ToArray());
        }

        public static void OnMainMenu()
        {
            _commandManager?.Dispose();
            _commandManager = null;
            _interfaceManager.DisableAll();
//            _interfaceManager.Enable(nameof(InGameMenu));
            _interfaceManager.Enable(nameof(Background));
            _interfaceManager.Enable(nameof(Loading));
            _interfaceManager.Enable(nameof(MainMenu));
        }

        public static void OnJoinedGame()
        {
            _commandManager = new CommandManager();
            _interfaceManager.DisableAll();
            _interfaceManager.Enable(nameof(Chat));
            _interfaceManager.Enable(nameof(Scoreboard));
            _interfaceManager.Enable(nameof(GameInfo));
        }

        public static bool TryFind(string name, out GameObject go)
        {
            if ((go = GameObject.Find(name)) != null)
                return true;
            return false;
        }

        #region Static methods

        public static void Log(params object[] messages)
        {
            foreach (var obj in messages)
                Log(obj);
        }

        public static void Log(object msg)
        {

        }

        public static Texture2D GetImage(string image)
        {
            if (Assembly.GetManifestResourceInfo($@"Mod.Resources.{image}.png") == null) return null;
            using (var stream = Assembly.GetManifestResourceStream($@"Mod.Resources.{image}.png"))
            {
                var texture = new Texture2D(0, 0, TextureFormat.RGBA32, false);
                texture.LoadImage(stream.ToBytes());
                texture.Apply();
                return texture;
            }
        }

        #endregion

        public static AnimationInfo Animation => _animation;
        
        public static ProfileManager ProfileManager => _profileManager;
        public static AnimationManager AnimationManager => _animationManager;
        public static InterfaceManager InterfaceManager => _interfaceManager;
        public static CommandManager CommandManager => _commandManager;
    }
}
