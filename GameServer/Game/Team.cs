﻿using System;
using System.Collections.Generic;
using System.Linq;
using Pangya_GameServer.GameType;
using Pangya_GameServer.Session;
using PangyaAPI.Utilities;
using PangyaAPI.Utilities.Log;
using _smp = PangyaAPI.Utilities.Log;

namespace Pangya_GameServer.Game
{

    public class Team
    {
        public class team_ctx
        {
            public team_ctx(uint _ul = 0u)
            {
                clear();
            }
            public void clear()
            {
            }
            public uint point = new uint(); // Aqui é a pontuação do time
            public ushort degree; // Angulo do team
            public byte player_start_hole; // Player que começou o Hole
            public byte acerto_hole; // Flag que acertou o hole
            public byte hole; // Hole que o team está
            public byte win = 1; // Flag que fala se o player ganhou o hole anterior
            public ushort finish; // State finish Hole, 9 finish with int putt, 10 finish with chip-in
            public byte quit = 1; // Player ou o Team desistiu
            public GameData data = new GameData();
            public GameType.Location location = new GameType.Location();
        }

        public Team(in int _id)
        {
            this.m_id = _id;
            this.m_players = new List<Player>();
            this.m_team_ctx = new team_ctx();
            this.m_player_turn = null;
        }

        public virtual void Dispose()
        {
            clear_players();
        }

        public void addPlayer(Player _player)
        {
            {
                if ((_player) == null)
                {
                    throw new exception("[Team::" + "addPlayer" + "][Error] _player is invalid(nullptr)", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.TEAM,
                        1, 0));
                }
                if (!(_player).getState() || !(_player).isConnected())
                {
                    throw new exception("[Team::" + "addPlayer" + "][Error] _player is not connected.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.TEAM,
                        2, 0));
                }
            };

            var it = m_players.FirstOrDefault(_el =>
            {
                return _el.m_pi.uid == _player.m_pi.uid;
            });

            if (it != null) // Add um playe ao team
            {
                m_players.Add(_player);
            }
            else
            {
                _smp.message_pool.push(new message("[Team::addPlayer][WARNING] player[UID=" + Convert.ToString(_player.m_pi.uid) + "] ja esta no team.", type_msg.CL_FILE_LOG_AND_CONSOLE));
            }

        }

        public void deletePlayer(Player _player, int _option)
        {
            {
                if ((_player) == null)
                {
                    throw new exception("[Team::" + "deletePlayer" + "][Error] _player is invalid(nullptr)", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.TEAM,
                        1, 0));
                }
                if ((!(_player).getState() || !(_player).isConnected()) && (_option) != 3)
                {
                    throw new exception("[Team::" + "deletePlayer" + "][Error] _player is not connected.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.TEAM,
                        2, 0));
                }
            };

            var it = m_players.FirstOrDefault(_el =>
            {
                return _el.m_pi.uid == _player.m_pi.uid;
            });

            if (it != null)  // deleta o player do map
            {
                m_players.Remove(it);
            }
            else
            {
                _smp.message_pool.push(new message("[Team::deletePlayer][WARNING] player[UID=" + Convert.ToString(_player.m_pi.uid) + "] ja foi deletado o map ou nunca esteve no map.", type_msg.CL_FILE_LOG_AND_CONSOLE));
            }

        }

        // finders

        // finders
        public Player findPlayerByOID(int _oid)
        {
            var it = m_players.FirstOrDefault(_el =>
           {
               return _el.m_oid == _oid;
           });

            return it;
        }

        public Player findPlayerByUID(uint _uid)
        {
            var it = m_players.FirstOrDefault(_el =>
            {
                return _el.m_pi.uid == _uid;
            });

            return it;
        }

        public Player findPlayerByNickname(string _nickname)
        {

            var it = m_players.FirstOrDefault(_el =>
            {
                return string.CompareOrdinal(_nickname, _el.m_pi.nickname) == 0;
            });

            return it;
        }

        // Gets and Sets
        public List<Player> getPlayers()
        {
            return m_players;
        }

        public uint getNumPlayers()
        {
            return (uint)m_players.Count();
        }


        // Gets and Sets
        public int getId()
        {
            return m_id;
        }

        public void setId(in int _id)
        {
            m_id = _id;
        }

        public uint getPoint()
        {
            return m_team_ctx.point;
        }

        public void setPoint(in uint _point)
        {
            m_team_ctx.point = _point;
        }

        public ushort getDegree()
        {
            return m_team_ctx.degree;
        }

        public void setDegree(in ushort _degree)
        {
            m_team_ctx.degree = _degree;
        }

        public GameType.Location getLocation()
        {
            return m_team_ctx.location;
        }

        public void setLocation(in GameType.Location _location)
        {
            m_team_ctx.location = _location;
        }

        public byte getAcertoHole()
        {
            return m_team_ctx.acerto_hole;
        }

        public void setAcertoHole(in byte _acerto_hole)
        {
            m_team_ctx.acerto_hole = _acerto_hole;
        }

        public byte getHole()
        {
            return m_team_ctx.hole;
        }

        public void setHole(in byte _hole)
        {
            m_team_ctx.hole = _hole;
        }

        public byte getGiveUp()
        {
            return m_team_ctx.data.giveup;
        }

        public void setGiveUp(in byte _giveup)
        {
            m_team_ctx.data.giveup = _giveup;
        }

        public uint getTimeout()
        {
            return m_team_ctx.data.time_out;
        }

        public void setTimeout(in uint _timeout)
        {
            m_team_ctx.data.time_out = _timeout;
        }

        public uint getTacadaNum()
        {
            return m_team_ctx.data.tacada_num;
        }

        public void setTacadaNum(in uint _tacada_num)
        {
            m_team_ctx.data.tacada_num = _tacada_num;
        }

        public uint getTotalTacadaNum()
        {
            return m_team_ctx.data.total_tacada_num;
        }

        public void setTotalTacadaNum(in uint _total_tacada_num)
        {
            m_team_ctx.data.total_tacada_num = _total_tacada_num;
        }

        public ulong getPang()
        {
            return m_team_ctx.data.pang;
        }

        public void setPang(in ulong _pang)
        {
            m_team_ctx.data.pang = _pang;
        }

        public ulong getBonusPang()
        {
            return m_team_ctx.data.bonus_pang;
        }

        public void setBonusPang(in ulong _bonus_pang)
        {
            m_team_ctx.data.bonus_pang = _bonus_pang;
        }

        public uint getBadCondute()
        {
            return m_team_ctx.data.bad_condute;
        }

        public void setBadCondute(in uint _bad_condute)
        {
            m_team_ctx.data.bad_condute = _bad_condute;
        }

        public int getScore()
        {
            return m_team_ctx.data.score;
        }

        public void setScore(in int _score)
        {
            m_team_ctx.data.score = _score;
        }

        public byte getLastWin()
        {
            return m_team_ctx.win;
        }

        public void setLastWin(in byte _win)
        {
            m_team_ctx.win = _win;
        }

        public byte getPlayerStartHole()
        {
            return m_team_ctx.player_start_hole;
        }

        public void setPlayerStartHole(in byte _player_start_hole)
        {
            m_team_ctx.player_start_hole = _player_start_hole;
        }

        public ushort getStateFinish()
        {
            return m_team_ctx.finish;
        }

        public void setStateFinish(in ushort _finish)
        {
            m_team_ctx.finish = _finish;
        }

        // Team desistiu
        public byte isQuit()
        {
            return m_team_ctx.quit;
        }

        public void setQuit(in byte _quit)
        {
            m_team_ctx.quit = _quit;
        }

        // increment

        // increment
        public void incrementTacadaNum(uint _inc = 1)
        {
            m_team_ctx.data.tacada_num += _inc;
        }

        public void incrementTotalTacadaNum(uint _inc = 1)
        {
            m_team_ctx.data.total_tacada_num += _inc;
        }

        public void incrementPlayerStartHole(byte _inc = 1)
        {
            m_team_ctx.player_start_hole += _inc;
        }

        public void incrementPoint(uint _inc = 1)
        {
            m_team_ctx.point += _inc;
        }

        public void incrementBadCondute(uint _inc = 1)
        {
            m_team_ctx.data.bad_condute += _inc;
        }

        public void incrementPang(ulong _inc = 1l)
        {
            m_team_ctx.data.pang += _inc;
        }

        public void incrementBonusPang(ulong _inc = 1l)
        {
            m_team_ctx.data.bonus_pang += _inc;
        }

        // decrement

        // decrement
        public void decrementPlayerStartHole(byte _dec = 1)
        {
            m_team_ctx.player_start_hole -= _dec;
        }

        // retorna o número de players no team(time)
        public uint getCount()
        {
            return (uint)m_players.Count();
        }

        // Requests
        public Player requestCalculePlayerTurn(uint _seq_hole)
        {

            if (_seq_hole > 0)
            {
                _seq_hole--;
            }

            m_player_turn = m_players[(int)(_seq_hole + m_team_ctx.player_start_hole) % m_players.Count()];

            return m_player_turn;
        }

        // Sort
        public void sort_player(uint _uid)
        {

            if (_uid == ~0u)
            {
                throw new exception("[Team::sort_player][Error] _uid is invalid(zero)", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.TEAM,
                    4, 0));
            }

            m_players.Sort((el1, el2) =>
            {
                if (el1.m_pi.uid == _uid && el2.m_pi.uid != _uid)
                {
                    return -1; // el1 vem antes de el2
                }
                if (el1.m_pi.uid != _uid && el2.m_pi.uid == _uid)
                {
                    return 1; // el1 vem depois de el2
                }
                return 0; // el1 e el2 são iguais no critério
            });

        }

        protected virtual void clear_players()
        {
            if (m_players.Any())
            {
                m_players.Clear();
            }
        }

        protected List<Player> m_players = new List<Player>();

        protected Player m_player_turn;

        protected int m_id = new int(); // Cor, 0 Red, 1 Blue

        // Dados of team
        protected team_ctx m_team_ctx = new team_ctx();
    }
}
