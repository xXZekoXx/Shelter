﻿using System;
using Mod.Exceptions;
using Mod.Interface;

namespace Mod.Commands
{
    public class CommandRevive : Command
    {
        public override string CommandName => "revive";
        public override string[] Aliases => new[] {"respawn"};

        public override void Execute(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0].EqualsIgnoreCase("all"))
                {
                    foreach (PhotonPlayer player in PhotonNetwork.playerList)
                        FengGameManagerMKII.instance.photonView.RPC("respawnHeroInNewRound", player);
                    Notify.New("Successfully respawned all players!", string.Empty, 1300, 35F);
                }
                else
                {
                    PhotonPlayer player = PhotonPlayer.Find(args[0].ToInt());
                    if (player == null)
                        throw new PlayerNotFoundException(args[0].ToInt());
                    FengGameManagerMKII.instance.photonView.RPC("respawnHeroInNewRound", player);
                    Notify.New($"{player.HexName} respawned!", string.Empty, 1300, 35F);
                }
            }
            else
            {
                FengGameManagerMKII.instance.respawnHeroInNewRound();
                Notify.New("Respawn forced!", string.Empty, 1300, 35F);
            }
        }
    }
}
