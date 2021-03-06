using System.Collections.Generic;
using Mod;
using Photon;

namespace RC
{
    public class RCEvent
    {
        private RCCondition condition;
        private RCAction elseAction;
        private int eventClass;
        private int eventType;
        public string foreachVariableName;
        public List<RCAction> trueActions;

        public RCEvent(RCCondition sentCondition, List<RCAction> sentTrueActions, int sentClass, int sentType)
        {
            return;
            this.condition = sentCondition;
            this.trueActions = sentTrueActions;
            this.eventClass = sentClass;
            this.eventType = sentType;
        }

        public void checkEvent()
        {
            return;
            int num2;
            switch (this.eventClass)
            {
                case 0:
                    for (num2 = 0; num2 < this.trueActions.Count; num2++)
                    {
                        this.trueActions[num2].doAction();
                    }
                    break;

                case 1:
                    if (!this.condition.checkCondition())
                    {
                        elseAction?.doAction();
                        break;
                    }
                    for (num2 = 0; num2 < this.trueActions.Count; num2++)
                    {
                        this.trueActions[num2].doAction();
                    }
                    break;

                case 2:
                    switch (this.eventType)
                    {
                        case 0:
                            foreach (TITAN titan in GameManager.instance.GetTitans())
                            {
                                if (GameManager.titanVariables.ContainsKey(this.foreachVariableName))
                                {
                                    GameManager.titanVariables[this.foreachVariableName] = titan;
                                }
                                else
                                {
                                    GameManager.titanVariables.Add(this.foreachVariableName, titan);
                                }
                                foreach (RCAction action in this.trueActions)
                                {
                                    action.doAction();
                                }
                            }
                            return;

                        case 1:
                            foreach (Player player in PhotonNetwork.PlayerList)
                            {
                                if (GameManager.playerVariables.ContainsKey(this.foreachVariableName))
                                {
                                    GameManager.playerVariables[this.foreachVariableName] = player;
                                }
                                else
                                {
                                    GameManager.titanVariables.Add(this.foreachVariableName, player);
                                }
                                foreach (RCAction action in this.trueActions)
                                {
                                    action.doAction();
                                }
                            }
                            return;
                    }
                    break;

                case 3:
                    while (this.condition.checkCondition())
                    {
                        foreach (RCAction action in this.trueActions)
                        {
                            action.doAction();
                        }
                    }
                    break;
            }
        }

        public void setElse(RCAction sentElse)
        {
            return;
            this.elseAction = sentElse;
        }
    }
}

