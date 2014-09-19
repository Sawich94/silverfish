﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HREngine.Bots
{

    public class EnemyTurnSimulator
    {

        private List<Playfield> posmoves = new List<Playfield>(7000);
        public int maxwide = 20;
        Movegenerator movegen = Movegenerator.Instance;

        public void simulateEnemysTurn(Playfield rootfield, bool simulateTwoTurns, bool playaround, bool print, int pprob, int pprob2)
        {
            bool havedonesomething = true;
            posmoves.Clear();
            if (print)
            {
                Helpfunctions.Instance.ErrorLog("board at enemyturn start-----------------------------");
                rootfield.printBoard();
            }
            posmoves.Add(new Playfield(rootfield));
            //posmoves[0].prepareNextTurn(false);
            List<Playfield> temp = new List<Playfield>();
            int deep = 0;
            int enemMana = Math.Min(rootfield.enemyMaxMana + 1, 10);

            if (playaround && !rootfield.loatheb)
            {
                float oldval = Ai.Instance.botBase.getPlayfieldValue(posmoves[0]);
                posmoves[0].value = int.MinValue;
                enemMana = posmoves[0].EnemyCardPlaying(rootfield.enemyHeroName, enemMana, rootfield.enemyAnzCards, pprob, pprob2);
                float newval = Ai.Instance.botBase.getPlayfieldValue(posmoves[0]);
                posmoves[0].value = int.MinValue;
                if (oldval < newval)
                {
                    posmoves.Clear();
                    posmoves.Add(new Playfield(rootfield));
                }
            }



            //play ability!
            if (posmoves[0].enemyAbilityReady && enemMana >= 2 && posmoves[0].enemyHeroAblility.card.canplayCard(posmoves[0], 0) && !rootfield.loatheb)
            {
                int abilityPenality = 0;

                havedonesomething = true;
                // if we have mage or priest, we have to target something####################################################
                if (posmoves[0].enemyHeroName == HeroEnum.mage || posmoves[0].enemyHeroName == HeroEnum.priest)
                {

                    List<Minion> trgts = posmoves[0].enemyHeroAblility.card.getTargetsForCardEnemy(posmoves[0]);
                    foreach (Minion trgt in trgts)
                    {
                        if (trgt.isHero) continue;
                        Action a = new Action(actionEnum.useHeroPower, posmoves[0].enemyHeroAblility, null, 0, trgt, abilityPenality, 0);
                        Playfield pf = new Playfield(posmoves[0]);
                        pf.doAction(a);
                        posmoves.Add(pf);
                    }
                }
                else
                {
                    // the other classes dont have to target####################################################
                    Action a = new Action(actionEnum.useHeroPower, posmoves[0].enemyHeroAblility, null, 0, null, abilityPenality, 0);
                    Playfield pf = new Playfield(posmoves[0]);
                    pf.doAction(a);
                    posmoves.Add(pf);
                }

            }


            foreach (Minion m in posmoves[0].enemyMinions)
            {
                if (m.Angr == 0) continue;
                m.numAttacksThisTurn = 0;
                m.playedThisTurn = false;
                m.updateReadyness();
            }

            doSomeBasicEnemyAi(posmoves[0]);

            int count = 0;
            //movegen...
            while (havedonesomething)
            {

                temp.Clear();
                temp.AddRange(posmoves);
                havedonesomething = false;
                Playfield bestold = null;
                float bestoldval = 20000000;
                foreach (Playfield p in temp)
                {

                    if (p.complete)
                    {
                        continue;
                    }

                    List<Action> actions = movegen.getEnemyMoveList(p, false, true, true, 1);// 1 for not using ability moves

                    foreach (Action a in actions)
                    {
                        havedonesomething = true;
                        Playfield pf = new Playfield(p);
                        pf.doAction(a);
                        posmoves.Add(pf);
                        count++;
                    }

                    //p.endCurrentPlayersTurnAndStartTheNextOne(1, false);
                    p.endEnemyTurn();
                    p.guessingHeroHP = rootfield.guessingHeroHP;
                    if (Ai.Instance.botBase.getPlayfieldValue(p) < bestoldval) // want the best enemy-play-> worst for us
                    {
                        bestoldval = Ai.Instance.botBase.getPlayfieldValue(p);
                        bestold = p;
                    }
                    posmoves.Remove(p);

                    if (count >= maxwide) break;
                }

                if (bestoldval <= 10000 && bestold != null)
                {
                    posmoves.Add(bestold);
                }

                deep++;
                if (count >= maxwide) break;
            }

            foreach (Playfield p in posmoves)
            {
                if (!p.complete) p.endEnemyTurn();
            }

            float bestval = int.MaxValue;
            Playfield bestplay = posmoves[0];

            foreach (Playfield p in posmoves)
            {
                p.guessingHeroHP = rootfield.guessingHeroHP;
                float val = Ai.Instance.botBase.getPlayfieldValue(p);
                if (bestval > val)// we search the worst value
                {
                    bestplay = p;
                    bestval = val;
                }
            }
            if (print)
            {
                Helpfunctions.Instance.ErrorLog("best enemy board----------------------------------");
                bestplay.printBoard();
            }
            rootfield.value = bestplay.value;
            if (simulateTwoTurns && bestplay.value > -1000)
            {
                bestplay.prepareNextTurn(true);
                rootfield.value = 0.5f * bestval + 0.5f * Ai.Instance.nextTurnSimulator.doallmoves(bestplay, false, print);
            }



        }

        CardDB.Card flame = CardDB.Instance.getCardDataFromID(CardDB.cardIDEnum.EX1_614t);

        private void doSomeBasicEnemyAi(Playfield p)
        {
            if (p.enemyHeroName == HeroEnum.mage)
            {
                if (Probabilitymaker.Instance.enemyCardsPlayed.ContainsKey(CardDB.cardIDEnum.EX1_561)) p.ownHero.Hp = Math.Max(5, p.ownHero.Hp - 7);
            }

            foreach (Minion m in p.enemyMinions.ToArray())
            {
                if (m.silenced) continue;
                if (p.enemyAnzCards >= 2 && (m.name == CardDB.cardName.gadgetzanauctioneer || m.name == CardDB.cardName.starvingbuzzard))
                {
                    if (p.enemyDeckSize >= 1)
                    {
                        p.drawACard(CardDB.cardName.unknown, false);
                    }
                }
                if (m.name == CardDB.cardName.northshirecleric)
                {
                    int anz = 0;
                    foreach (Minion mnn in p.enemyMinions)
                    {
                        if (mnn.wounded) anz++;
                    }
                    anz = Math.Min(anz, 3);
                    for (int i = 0; i < anz; i++)
                    {
                        if (p.enemyDeckSize >= 1)
                        {
                            p.drawACard(CardDB.cardName.unknown, false);
                        }
                    }
                }

                if (m.name == CardDB.cardName.illidanstormrage && p.enemyAnzCards >= 1)
                {
                    p.callKid(flame, p.enemyMinions.Count, false);
                }

                if (m.name == CardDB.cardName.questingadventurer && p.enemyAnzCards >= 1)
                {
                    p.minionGetBuffed(m, 1, 1);
                    if (p.enemyAnzCards >= 3 && p.enemyMaxMana >= 5)
                    {
                        p.minionGetBuffed(m, 1, 1);
                    }
                }

                if (m.name == CardDB.cardName.manaaddict && p.enemyAnzCards >= 1)
                {
                    p.minionGetTempBuff(m, 2, 0);
                    if (p.enemyAnzCards >= 3 && p.enemyMaxMana >= 5)
                    {
                        p.minionGetTempBuff(m, 2, 0);
                    }
                }

                if (m.name == CardDB.cardName.manawyrm && p.enemyAnzCards >= 1)
                {
                    p.minionGetBuffed(m, 1, 0);
                    if (p.enemyAnzCards >= 3 && p.enemyMaxMana >= 5)
                    {
                        p.minionGetBuffed(m, 1, 0);
                    }
                }

                if (m.name == CardDB.cardName.secretkeeper && p.enemyAnzCards >= 3)
                {
                    p.minionGetBuffed(m, 1, 1);
                }

                if (m.name == CardDB.cardName.unboundelemental && p.enemyAnzCards >= 2)
                {
                    p.minionGetBuffed(m, 1, 1);
                }

                if (m.name == CardDB.cardName.murloctidecaller && p.enemyAnzCards >= 2)
                {
                    p.minionGetBuffed(m, 1, 0);
                }

                if (m.name == CardDB.cardName.undertaker && p.enemyAnzCards >= 2)
                {
                    p.minionGetBuffed(m, 1, 1);
                }

                if (m.name == CardDB.cardName.frothingberserker && p.enemyMinions.Count + p.ownMinions.Count >= 3)
                {
                    p.minionGetBuffed(m, 1, 0);
                }

                if (m.name == CardDB.cardName.gurubashiberserker && m.Hp >= 5 && p.enemyAnzCards >= 3)
                {
                    p.minionGetBuffed(m, 3, 0);
                }

                if (m.name == CardDB.cardName.lightwarden)
                {
                    int anz = 0;
                    foreach (Minion mnn in p.enemyMinions)
                    {
                        if (mnn.wounded) anz++;
                    }
                    if (p.enemyHero.wounded) anz++;
                    if (anz >= 2) p.minionGetBuffed(m, 2, 0);
                }
            }

            if (p.enemyMinions.Count < 7)
            {
                p.callKid(this.flame, p.enemyMinions.Count, false);
                int bval = 1;
                if (p.enemyMaxMana > 4) bval = 2;
                if (p.enemyMaxMana > 7) bval = 3;
                p.minionGetBuffed(p.enemyMinions[p.enemyMinions.Count - 1], bval - 1, bval);
            }
        }


    }

}
