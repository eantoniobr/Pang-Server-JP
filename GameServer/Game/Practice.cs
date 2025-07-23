﻿// Arquivo Game.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Pangya_GameServer.Cmd;
using Pangya_GameServer.Game.Base;
using Pangya_GameServer.Game.Manager;
using Pangya_GameServer.Game.System;
using Pangya_GameServer.Game.Utils;
using Pangya_GameServer.GameType;
using Pangya_GameServer.PacketFunc;
using Pangya_GameServer.Session;
using Pangya_GameServer.UTIL;
using PangyaAPI.IFF.JP.Extensions;
using PangyaAPI.IFF.JP.Models.Flags;
using PangyaAPI.Network.PangyaPacket;
using PangyaAPI.SQL;
using PangyaAPI.SQL.Manager;
using PangyaAPI.Utilities;
using PangyaAPI.Utilities.BinaryModels;
using PangyaAPI.Utilities.Log;
using static Pangya_GameServer.GameType._Define;
using _smp = PangyaAPI.Utilities.Log;
using int32_t = System.Int32;
using int64_t = System.Int64;
using size_t = System.Int32;
using uint32_t = System.UInt32;
using uint64_t = System.UInt64;

namespace Pangya_GameServer.Game
{
    public class Practice : TourneyBase
    {
        bool m_practice_state;

        public Practice(List<Player> _players, RoomInfoEx _ri, RateValue _rv, bool _channel_rookie) : base(_players, _ri, _rv, _channel_rookie)
        {
            this.m_practice_state = false;

            // Aqui tem que inicializar os players info
            initAllPlayerInfo();

            m_state = init_game();
        }

        ~Practice()
        {         
            m_practice_state = false;
            
            _smp::message_pool.push(new message("[Practice::~Practice][Log] Practice destroyed on Room[Number=" + (m_ri.numero) + "]"));

        }

        public override void changeHole(Player _session)
        {

            updateTreasureHunterPoint(_session);

            if (checkEndGame(_session))
            {
                finish_practice(_session, 0);
            }
            else
            {
                // Resposta terminou o hole
                updateFinishHole(_session, 1);
            }
        }
        public override void finishHole(Player _session)
        {
            requestFinishHole(_session, 0);

            requestUpdateItemUsedGame(_session);

        }
        public override void requestInitHole(Player _session, packet _packet)
        {
            //REQUEST_BEGIN("InitHole");

            try
            {

                base.requestInitHole(_session, _packet);

                INIT_PLAYER_INFO("requestInitHole",
                    "tentou inicializar o hole do jogo",
                    _session, out PlayerGameInfo pgi);

                // Update Counter Hole do Achievement do player
                //  //pgi.sys_achieve.incrementCounter(0x6C400005u / *Hole Count * /);

            }
            catch (exception e)
            {

                _smp.message_pool.push(new message("[Practice::requestInitHole][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public override void requestCalculePang(Player _session)
        {

            base.requestCalculePang(_session);

            INIT_PLAYER_INFO("requestCalculePang",
                "tentou calcular o pang do player no jogo",
                _session, out PlayerGameInfo pgi);

            // Practice
            // Hole Repeat ganha 1/6 dos pang(s) feito
            // Course Practice ganha 1/3 dos pang(s) feito

            if (m_ri.modo == (int)RoomInfo.eMODO.M_REPEAT)
            { // Hole Repeat

                pgi.data.pang = (ulong)(pgi.data.pang * (1.0f / 6.0f));
                pgi.data.bonus_pang = (ulong)(pgi.data.bonus_pang * (1.0f / 6.0f));

            }
            else
            { // Course Practice

                pgi.data.pang = (ulong)(pgi.data.pang * (1.0f / 3.0f));
                pgi.data.bonus_pang = (ulong)(pgi.data.bonus_pang * (1.0f / 3.0f));
            }
        }

        public void finish_practice(Player _session, int _option)
        {

            if (m_players.Count > 0 && m_game_init_state == 1)
            {

                INIT_PLAYER_INFO("finish_practice",
                    "tentou terminar o practice no jogo",
                    _session, out PlayerGameInfo pgi);

                if (pgi.flag == PlayerGameInfo.eFLAG_GAME.PLAYING)
                {

                    // Calcula os pangs que o player ganhou
                    requestCalculePang(_session);

                    // Atualizar os pang do player se ele estiver com assist ligado, e for maior que beginner E
                    updatePlayerAssist(_session);

                    if (m_game_init_state == 1 && _option == 0)
                    {

                        // Mostra msg que o player terminou o jogo
                        sendFinishMessage(_session);

                        // Resposta terminou o hole
                        updateFinishHole(_session, 1);

                        // Resposta Terminou o Jogo, ou Saiu
                        sendUpdateState(_session, 2);

                        // Achievement Counter
                        //pgi.sys_achieve.incrementCounter((m_ri.modo == RoomInfo.MODO.M_REPEAT) ? 0x6C40003Du / *Hole Repeat * / : 0x6C40003Eu / *Course Practice * /);
                    }
                }

                //pgi->flag = (_option == 0) ? PlayerGameInfo::eFLAG_GAME::FINISH : PlayerGameInfo::eFLAG_GAME::END_GAME;
                setGameFlag(pgi, (_option == 0) ? PlayerGameInfo.eFLAG_GAME.FINISH : PlayerGameInfo.eFLAG_GAME.END_GAME);

                //pgi.time_finish.CreateTime();

                if (AllCompleteGameAndClear() && m_game_init_state == 1)
                {
                    finish(); // Envia os pacotes que termina o jogo Ex: 0xCE, 0x79 e etc
                }
            }

        }

        public override void requestChangeWindNextHoleRepeat(Player _session, packet _packet)
        {
            //REQUEST_BEGIN("ChangeWindNextHoleRepeat");

            try
            {

                INIT_PLAYER_INFO("requestChangeWindNextHoleRepeat",
                    "tentou trocar o vento dos proximos holes repeat no jogo",
                    _session, out PlayerGameInfo pgi);

                m_course.shuffleWindNextHole(pgi.hole);

            }
            catch (exception e)
            {

                _smp.message_pool.push(new message("[Practice::requestChangeWindNextHoleRepeat][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public override void timeIsOver()
        {

            try
            {

                // Block
                Monitor.Enter(m_cs_sync_finish_game);

                if (m_game_init_state == 1 && m_players.Count > 0)
                {

                    m_game_init_state = 2; // Acabou

                    var p = new PangyaBinaryWriter();

                    var s = m_players.First();

                    INIT_PLAYER_INFO("end_time",
                        "tentou acabar o tempo no jogo",
                        s, out PlayerGameInfo pgi);

                    requestCalculePang(s);

                    sendFinishMessage(s);

                    // Resposta terminou o hole
                    updateFinishHole(s, 0);

                    requestFinishHole(s, 1); // N o terminou o hole

                    // ------ O Original n o soma as tacadas do resto dos holes que o player n o jogou, quando o tempo acaba -------
                    //pgi->ui.tacada = pgi->data.total_tacada_num;

                    // Resposta para o termina Game por tempo
                    sendTimeIsOver(s);

                    // Termina jogo
                    finish();

                    if (m_timer != null)
                        clear_time();

                    _smp.message_pool.push(new message("[Practice::timeIsOver][Log] Tempo Acabou no Practice. na sala[NUMERO=" + Convert.ToString(m_ri.numero) + "]", type_msg.CL_FILE_LOG_AND_CONSOLE));
                }

                // Libera
                Monitor.Exit(m_cs_sync_finish_game);

            }
            catch (exception e)
            {
                Monitor.Exit(m_cs_sync_finish_game);


                _smp.message_pool.push(new message("[Practice::timeIsOver][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        protected override bool init_game()
        {

            if (m_players.Count > 0)
            {

                // Cria o timer do practice
                startTime();//nao sei bem o que é, mas e um tempo!

                // variavel que salva a data local do sistema
                initGameTime();

                m_game_init_state = 1; // Come ou Game

                m_practice_state = true; // Come ou Practice
            }

            return true;
        }

        protected override void requestReplySyncShotData(Player _session)
        {
            //CHECK_SESSION_BEGIN("requestReplySyncShotData");

            try
            {

                // Resposta Sync Shot
                sendSyncShot(_session);

            }
            catch (exception e)
            {

                _smp.message_pool.push(new message("[Practice::requestReplySyncShotData][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public void requestSavePang(Player _session)
        {

            INIT_PLAYER_INFO("requestSavePang",
                "tentou salvar os pang ganho no jogo",
                _session, out PlayerGameInfo pgi);

            if (pgi.data.pang > 0 || pgi.data.bonus_pang > 0) // S  add se for maior que 0
            {
                _session.m_pi.addPang(pgi.data.pang + pgi.data.bonus_pang);
            }
        }

        public void requestFinishExpGame()
        {

            if (m_players.Count > 0)
            {

                // Practine n o conta estrela ele da 1 de exp por hole jogados
                float stars = m_course.getStar();

                var hole_seq = 0;

                foreach (var el in m_players)
                {

                    INIT_PLAYER_INFO("requestFinishExpGame",
                        "tentou finalizar exp do jogo",
                        el, out PlayerGameInfo pgi);

                    hole_seq = (int)m_course.findHoleSeq(pgi.hole);

                    // Ele est  no primeiro hole e n o acertou ele, s  da experi ncia se ele tiver acertado o hole
                    if (hole_seq == 1 && !(pgi.shot_sync.state_shot.display.acerto_hole))
                    {
                        hole_seq = 0;
                    }

                    if (el.m_pi.level < 70)
                    {
                        pgi.data.exp = (uint)((hole_seq > 0 ? hole_seq : 0) * TRANSF_SERVER_RATE_VALUE((int)m_rv.exp) * TRANSF_SERVER_RATE_VALUE((int)pgi.used_item.rate.exp));
                    }

                    _smp.message_pool.push(new message("[Practice::requestFinishExpGame][Log] player[UID=" + Convert.ToString(el.m_pi.uid) + "] ganhou " + Convert.ToString(pgi.data.exp) + " de experience.", type_msg.CL_FILE_LOG_AND_CONSOLE));

                }
            }
        }

        public void finish()
        {

            m_game_init_state = 2; // Acabou o Jogo

            requestCalculeRankPlace();

            requestFinishExpGame();

            foreach (var el in m_players)
            {

                INIT_PLAYER_INFO("finish",
                    "tentou finalizar os dados do jogador no jogo",
                    el, out PlayerGameInfo pgi);

                if (pgi.flag != PlayerGameInfo.eFLAG_GAME.QUIT)
                {
                    requestFinishData(el);
                }
            }
        }

        public void requestFinishData(Player _session)
        {

            requestFinishItemUsedGame(_session);

            requestSaveDrop(_session);

            INIT_PLAYER_INFO("requestFinishData",
                "tentou finalizar dados do jogo",
                _session, out PlayerGameInfo pgi);

            // Resposta terminou game - Drop Itens
            sendDropItem(_session);

            // Resposta terminou game - Placar
            sendPlacar(_session);
        }

        public override int checkEndShotOfHole(Player _session)
        {

            // Agora verifica o se ele acabou o hole e essas coisas
            INIT_PLAYER_INFO("checkEndShotOfHole",
                "tentou verificar a ultima tacada do hole no jogo",
                _session, out PlayerGameInfo pgi);

            if (pgi.shot_sync.state_shot.display.acerto_hole || pgi.data.giveup == 1)
            {

                // Verifica se o player terminou jogo, fez o ultimo hole
                if (m_course.findHoleSeq(pgi.hole) == m_ri.qntd_hole)
                {

                    // Resposta para o player que terminou o ultimo hole do Game
                    var p = new PangyaBinaryWriter((ushort)0x199);

                    packet_func.session_send(p,
                        _session, 1);

                    // Fez o Ultimo Hole, Calcula Clear Bonus para o player
                    if (pgi.shot_sync.state_shot.display.clear_bonus)
                    {

                        if (!sMap.getInstance().isLoad())
                        {
                            sMap.getInstance().load();
                        }

                        var map = sMap.getInstance().getMap((byte)(m_ri.course & RoomInfo.eCOURSE.UNK));

                        if (map == null)
                        {
                            _smp.message_pool.push(new message("[TourneyBase::checkEndShotOfHole][Error][WARNING] tentou pegar o Map dados estaticos do course[COURSE=" + Convert.ToString((ushort)((byte)(m_ri.course & RoomInfo.eCOURSE.UNK))) + "], mas nao conseguiu encontra na classe do Server.", type_msg.CL_FILE_LOG_AND_CONSOLE));
                        }
                        else
                        {
                            pgi.data.bonus_pang += sMap.getInstance().calculeClear30s(map, m_ri.qntd_hole);
                        }
                    }
                }

                finishHole(_session);

                changeHole(_session);
            }

            return 0;
        }

        public override void requestCalculeShotSpinningCube(Player _session, ShotSyncData _ssd)
        {
            //CHECK_SESSION_BEGIN("requestCalculeShotSpinningCube");

            try
            {

#if _DEBUG
			// S  calcula em modo debug, por que practice n o pode contar
			calcule_shot_to_spinning_cube(_session, _ssd);
#endif // _DEBUG

            }
            catch (exception e)
            {

                _smp.message_pool.push(new message("[Practice::requestCalculeShotSpinningCube][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public override void requestCalculeShotCoin(Player _session, ShotSyncData _ssd)
        {
            //CHECK_SESSION_BEGIN("requestCalculeShotCoin");

            try
            {

#if _DEBUG
			// S  calcule em modo debug, por que practice n o pode contar
			calcule_shot_to_coin(_session, _ssd);
#endif // _DEBUG

            }
            catch (exception e)
            {

                _smp.message_pool.push(new message("[Practice::requestCalculeShotCoin][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public override bool finish_game(Player _session, int option)
        {
            if (_session.getState()
            && _session.isConnected()
            && m_players.Count() > 0)
            {

                if (option == 6)
                {

                    var p = new PangyaBinaryWriter();

                    if (m_practice_state)
                    {
                        finish_practice(_session, 1); // Termina sem ter acabado de jogar
                    }

                    INIT_PLAYER_INFO("finish_game",
                        "tentou terminar o jogo",
                        _session, out PlayerGameInfo pgi);

                    // Practice n o salva info, s  pang, exp, e itens used and dropped. Ex: Active Item, Spinning Cube e Coin
                    requestSavePang(_session);

                    // Practice ganha exp, mas nao d  para o caddie, o caddie n o ganha exp no practice
                    if (pgi.data.exp > 0) // s  add exp se for maior que 0
                    {
                        _session.addExp(pgi.data.exp, false);
                    }

                    // PRACTICE N O DA EXP PARA CADDIE E NEM MASCOT
                    // Depois tem que add nos outros modos de jogo o add _session.addCaddieExp

                    // Depois tem que add nos outros modos de jogo o add _session.addMascotExp

                    // Update Info Map Statistics
                    sendUpdateInfoAndMapStatistics(_session, 0);

                    // Resposta Treasure Hunter Item
                    requestSendTreasureHunterItem(_session);

                    // Achievement Aqui
                    //pgi.sys_achieve.finish_and_update(_session);

                    // Resposta Update Pang
                    p.init_plain((ushort)0xC8);

                    p.WriteUInt64(_session.m_pi.ui.pang);

                    p.WriteUInt64(0Ul);

                    packet_func.session_send(p,
                        _session, 1);

                    // Colocar o finish_game Para 1 quer dizer que ele acabou o camp
                    pgi.finish_game = 1;

                    // Flag do game que terminou
                    m_game_init_state = 2; // ACABOU
                }
            }

            return (PlayersCompleteGameAndClear() && m_practice_state);
        }

    }
}
