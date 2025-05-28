﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using Pangya_GameServer.Cmd;
using Pangya_GameServer.Game;
using Pangya_GameServer.Game.Manager;
using Pangya_GameServer.Game.System;
using Pangya_GameServer.Game.Utils;
using Pangya_GameServer.GameType;
using Pangya_GameServer.PacketFunc;
using Pangya_GameServer.PangyaEnums;
using Pangya_GameServer.Session;
using Pangya_GameServer.UTIL;
using PangyaAPI.IFF.JP.Extensions;
using PangyaAPI.Network.Cmd;
using PangyaAPI.Network.Pangya_St;
using PangyaAPI.Network.PangyaPacket;
using PangyaAPI.Network.PangyaServer;
using PangyaAPI.SQL;
using PangyaAPI.SQL.Manager;
using PangyaAPI.Utilities;
using PangyaAPI.Utilities.BinaryModels;
using PangyaAPI.Utilities.Log;
using static Pangya_GameServer.GameType._Define;
using _smp = PangyaAPI.Utilities.Log;

namespace Pangya_GameServer.GameServerTcp
{
    public partial class GameServer : Server
    {
        public int m_access_flag { get; private set; }
        public int m_create_user_flag { get; private set; }
        public int m_same_id_login_flag { get; private set; }
        DailyQuestInfo m_dqi;
        protected List<Channel> v_channel;
        public BroadcastList m_ticker;
        public BroadcastList m_notice;
        static player_manager m_player_manager = new player_manager();
        public GameServer() : base(m_player_manager)
        {
            // Inicilializa Thread que cuida de verificar todos os itens do players, estão com o tempo normal
            var m_threads = new Thread(check_player);

            m_threads.Start();
             
            // Inicializa config do Game Server
            config_init();
            // init Request Client packets
            init_Packets();
            //init create/load channels
            init_load_channels();
            // Inicializa os sistemas Globais
            init_systems();

            // Initialized complete
            m_state = ServerState.Initialized;

        }


        public override void config_init()
        {
            base.config_init();

            // Server Tipo
            m_si.tipo = 1;

            m_si.img_no = m_reader_ini.ReadInt16("SERVERINFO", "ICONINDEX");
            m_si.rate.exp = (short)m_reader_ini.readInt("SERVERINFO", "EXPRATE");
            m_si.rate.scratchy = (short)m_reader_ini.readInt("SERVERINFO", "SCRATCHY_RATE");
            m_si.rate.pang = (short)m_reader_ini.readInt("SERVERINFO", "PANGRATE");
            m_si.rate.club_mastery = (short)m_reader_ini.readInt("SERVERINFO", "CLUBMASTERYRATE");
            m_si.rate.papel_shop_rare_item = (short)m_reader_ini.readInt("SERVERINFO", "PAPEL_rate_RATE"); ;
            m_si.rate.papel_shop_cookie_item = (short)m_reader_ini.readInt("SERVERINFO", "PAPEL_COOKIE_ITEM_RATE"); ;
            m_si.rate.treasure = (short)m_reader_ini.readInt("SERVERINFO", "TREASURE_RATE"); ;
            m_si.rate.memorial_shop = (short)m_reader_ini.readInt("SERVERINFO", "MEMORIAL_RATE");
            m_si.rate.chuva = (short)m_reader_ini.readInt("SERVERINFO", "CHUVA_RATE");
            m_si.rate.grand_zodiac_event_time = (short)(m_reader_ini.readInt("SERVERINFO", "GZ_EVENT") >= 1 ? 1 : 0);// Ativo por padrão
            m_si.rate.grand_prix_event = (short)(m_reader_ini.readInt("SERVERINFO", "GP_EVENT") >= 1 ? 1 : 0);// Ativo por padrão
            m_si.rate.golden_time_event = ((short)(m_reader_ini.readInt("SERVERINFO", "GOLDEN_TIME_EVENT") >= 1 ? 1 : 0));// Ativo por padrão
            m_si.rate.login_reward_event = ((short)(m_reader_ini.readInt("SERVERINFO", "LOGIN_REWARD") >= 1 ? 1 : 0));// Ativo por padrão
            m_si.rate.bot_gm_event = ((short)(m_reader_ini.readInt("SERVERINFO", "BOT_GM_EVENT") >= 1 ? 1 : 0));// Ativo por padrão
            m_si.rate.smart_calculator = (/*m_reader_ini.readInt("SERVERINFO", "SMART_CALC") >= 1 ? true :*/ 0);// Atibo por padrão
            m_si.rate.angel_event = ((short)(m_reader_ini.readInt("SERVERINFO", "ANGEL_EVENT") >= 1 ? 1 : 0));// Atibo por padrão
            try
            {

                m_si.flag.ullFlag = m_reader_ini.ReadUInt64("SERVERINFO", "FLAG");

            }
            catch (exception e)
            {

                _smp.message_pool.push(("[GameServer.config_init][ErrorSystem] Config.FLAG" + e.getFullMessageError()));
            }


            // Recupera Valores de rate do gs do banco de dados
            var cmd_rci = new CmdRateConfigInfo(m_si.uid);  // Waiter

            if (cmd_rci.getException().getCodeError() != 0 || cmd_rci.isError()/*Deu erro na consulta não tinha o rate config info para esse gs, pode ser novo*/)
            {

                if (cmd_rci.getException().getCodeError() != 0)
                    _smp.message_pool.push(("[GameServer.config_init][ErrorSystem] " + cmd_rci.getException().getFullMessageError()));


                setAngelEvent(m_si.rate.angel_event);
                setratePang(m_si.rate.pang);
                setrateExp(m_si.rate.exp);
                setrateClubMastery(m_si.rate.club_mastery);
            }
            else
            {   // Conseguiu recuperar com sucesso os valores do gs

                setAngelEvent(m_si.rate.angel_event);
                setratePang(m_si.rate.pang);
                setrateExp(m_si.rate.exp);
                setrateClubMastery(m_si.rate.club_mastery);
            }
            m_si.app_rate = 100;    // Esse aqui nunca usei, deixei por que no DB do s4 tinha só cópiei
        }
        public bool getAccessFlag()
        {
            return m_access_flag == 1;
        }

        public bool getCreateUserFlag()
        {
            return m_create_user_flag == 1;
        }

        public bool canSameIDLogin()
        {
            return m_same_id_login_flag == 1;
        }
        // Set Event Server
        private void setAngelEvent(short _angel_event)
        {
            // Evento para reduzir o quit rate, diminui 1 quit a cada jogo concluído
            m_si.event_flag.angel_wing = _angel_event > 0;
            // Update rate Pang
            m_si.rate.angel_event = _angel_event; //precisa fazer isso, pois pode querer desativar
        }
        private void setratePang(short _pang)
        {
            // Update Flag Event
            m_si.event_flag.pang_x_plus = (_pang >= 200) ? true : false;

            // Update rate Pang
            m_si.rate.pang = _pang;
        }
        private void setrateExp(short _exp)
        {// Reseta flag antes de atualizar ela 
            m_si.event_flag.exp_x2 = m_si.event_flag.exp_x_plus = false;

            // Update Flag Event
            if (_exp > 200)
                m_si.event_flag.exp_x_plus = true;
            else if (_exp == 200)
                m_si.event_flag.exp_x2 = true;
            else
                m_si.event_flag.exp_x2 = m_si.event_flag.exp_x_plus = false;

            // Update rate Experiência
            m_si.rate.exp = _exp;
        }
        private void setrateClubMastery(short _club_mastery)
        {
            // Update Flag Event
            m_si.event_flag.club_mastery_x_plus = (_club_mastery >= 200) ? true : false;

            // Update rate Club Mastery
            m_si.rate.club_mastery = _club_mastery;
        }

        public override void OnHeartBeat()
        {
            try
            {
                // Server ainda não está totalmente iniciado
                if (!this._isRunning)
                    return;

                // Check Invite Time Channels
                foreach (var el in v_channel)
                    el.checkInviteTime();

                // Begin Check System Singleton Static
                // Carrega IFF_STRUCT
                if (!sIff.getInstance().isLoad())
                    sIff.getInstance().load();

                //// Map Dados Estáticos
                if (!sMap.getInstance().isLoad())
                    sMap.getInstance().load();

                // Carrega Card System
                if (!sCardSystem.getInstance().isLoad())
                    sCardSystem.getInstance().load();

                //// Carrega Comet Refill System
                if (!sCometRefillSystem.getInstance().isLoad())
                    sCometRefillSystem.getInstance().load();

                // Carrega Papel Shop System
                if (!sPapelShopSystem.getInstance().isLoad())
                    sPapelShopSystem.getInstance().load();

                //// Carrega Box System
                //if (!sBoxSystem.getInstance().isLoad())
                //    sBoxSystem.getInstance().load();

                //// Carrega Memorial System
                if (!sMemorialSystem.getInstance().isLoad())
                    sMemorialSystem.getInstance().load();

                //// Carrega Cube Coin System(SobreCarga)
                if (!sCubeCoinSystem.getInstance().isLoad())
                    sCubeCoinSystem.getInstance().load();

                //// Treasure Hunter System
                if (!sTreasureHunterSystem.getInstance().isLoad())
                    sTreasureHunterSystem.getInstance().load();

                //// Drop System
                if (!sDropSystem.getInstance().isLoad())
                    sDropSystem.getInstance().load();

                // Attendance Reward System
                if (!sAttendanceRewardSystem.getInstance().isLoad())
                    sAttendanceRewardSystem.getInstance().load();

                //// Approach Mission
                //if (!sApproachMissionSystem.getInstance().isLoad())
                //    sApproachMissionSystem.getInstance().load();

                //// Grand Zodiac Event
                //if (!sGrandZodiacEvent.getInstance().isLoad())
                //    sGrandZodiacEvent.getInstance().load();

                //// Coin Cube Location System
                //if (!sCoinCubeLocationUpdateSystem.getInstance().isLoad())
                //    sCoinCubeLocationUpdateSystem.getInstance().load();

                //// Golden Time System
                //if (!sGoldenTimeSystem.getInstance().isLoad())
                //    sGoldenTimeSystem.getInstance().load();

                //// Login Reward System
                if (!sLoginRewardSystem.getInstance().isLoad())
                    sLoginRewardSystem.getInstance().load();

                //// Carrega Smart Calculator Lib, Só inicializa se ele estiver ativado
                //if (m_si.rate.smart_calculator && !sSmartCalculator.getInstance().hasStopped() && !sSmartCalculator.getInstance().isLoad())
                //    sSmartCalculator.getInstance().load();

                //// End Check System Singleton Static

                //// check Grand Zodiac Event Time
                //if (m_si.rate.grand_zodiac_event_time && sGrandZodiacEvent.getInstance().checkTimeToMakeRoom())
                //    makeGrandZodiacEventRoom();

                //// check Bot GM Event Time
                if (m_si.rate.bot_gm_event == 1 && sBotGMEvent.getInstance().checkTimeToMakeRoom())
                    makeBotGMEventRoom();

                //// check Golden Time Round Update
                //if (m_si.rate.golden_time_event && sGoldenTimeSystem.getInstance().checkRound())
                //    makeListOfPlayersToGoldenTime();

                //// update Login Reward
                if (m_si.rate.login_reward_event == 1)
                    sLoginRewardSystem.getInstance().updateLoginReward();

                //// Check Daily Quest
                //if (MgrDailyQuest.checkCurrentQuest(m_dqi))
                //    MgrDailyQuest.updateDailyQuest(m_dqi);  // Atualiza daily quest

                //// Check Update Dia do Papel Shop System
                //sPapelShopSystem.getInstance().updateDia();

                //if (sTreasureHunterSystem.getInstance().checkUpdateTimePointCourse())
                //{

                //    packet p;

                //    packet_func.pacote131(p);

                //    foreach (var el in v_channel)
                //        packet_func.channel_broadcast(el, p, 1);
                //}
                //// End Check Treasure Hunter

                //// Check Notice (GM or Cube Win Rare)
                //BroadcastList.RetNoticeCtx rt;

                //rt = m_notice.peek();

                //if (rt.ret == BroadcastList.RET_TYPE.OK)
                //{

                //    packet p;

                //    if (rt.nc.type == BroadcastList.TYPE.GM_NOTICE)
                //    {    // GM Notice

                //        p.init_plain((unsigned short)0x42);

                //        p.WriteString(rt.nc.notice);

                //    }
                //    else if (rt.nc.type == BroadcastList.TYPE.CUBE_WIN_RARE)
                //    {   // Cube Win Rare Notice

                //        p.init_plain((unsigned short)0x1D3);

                //        p.WriteUint32(1);             // Count

                //        //for (auto i = 0u; i < 2u; ++i) {
                //        p.WriteUint32(rt.nc.option);
                //        p.WriteString(rt.nc.notice);
                //        //}

                //    }

                //    // Broadcast to All Channels
                //    foreach (var el in v_channel)
                //        packet_func.channel_broadcast(el, p, 1);
                //}

                //// Check Ticker
                //rt = m_ticker.peek();

                //if (rt.ret == BroadcastList.RET_TYPE.OK && rt.nc.type == BroadcastList.TYPE.TICKER)
                //{   // Ticker Msg

                //    packet p((unsigned short)0xC9);

                //    p.WriteString(rt.nc.nickname);
                //    p.WriteString(rt.nc.notice);

                //    // Broadcast to All Channels
                //    foreach (var el in v_channel)
                //        packet_func.channel_broadcast(el, p, 1);
                //}

            }
            catch (exception e)
            {
                _smp.message_pool.push("[GameServer.onHeartBeat][ErrorSystem] " + e.getFullMessageError(), _smp.type_msg.CL_FILE_LOG_AND_CONSOLE);
            }
        }

        public override void SQLDBResponse(int _msg_id, Pangya_DB _pangya_db, object _arg)
        {
            if (_pangya_db is CmdServerList cmdServerList)
            {
                base.SQLDBResponse(_msg_id, cmdServerList, _arg);
                return;
            }


            if (_arg == null)
            {
                _smp.message_pool.push("[GameServer.SQLDBResponse][Error] _arg is null na msg_id = " + (_msg_id));
                return;
            }

            // Por Hora só sai, depois faço outro tipo de tratamento se precisar
            if (_pangya_db.getException().getCodeError() != 0)
                throw new exception("[GameServer.SQLDBResponse][Error] " + _pangya_db.getException().getFullMessageError());

            switch (_msg_id)
            {

                default:
                    {
                        break;
                    }
            }
        }


        public virtual void destroyRoom(byte _channel_owner, short _number)
        {

        }

        public virtual void sendServerListAndChannelListToSession(Player _session)
        {
            packet_func.session_send(packet_func.pacote09F(m_server_list, v_channel), _session);
        }

        public virtual void sendDateTimeToSession(Player _session)
        {
            using (var p = new PangyaBinaryWriter((ushort)0xBA))
            {
                p.WriteTime();
                packet_func.session_send(p, _session);
            }
        }

        public virtual void sendRankServer(Player _session)
        {

            try
            {

                //if (_session.m_pi.block_flag.m_flag.rank_server)
                //    throw new exception("[GameServer.sendRankServer][Error] Player[UID=" + (_session.m_pi.m_uid)
                //            + "] esta bloqueado o Rank Server, ele nao pode acessar o rank server.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 7010, 0));

                var cmd_sl = new CmdServerList(TYPE_SERVER.RANK);   // Waiter

                NormalManagerDB.add(0, cmd_sl, null, null);

                if (cmd_sl.getException().getCodeError() != 0)
                    throw cmd_sl.getException();

                var sl = cmd_sl.getServerList();

                if (sl.Count == 0)
                    throw new exception("[GameServer.sendRankServer][WARNING] Player[UID=" + (_session.m_pi.uid)
                            + "] requisitou o Rank Server, mas nao tem nenhum Rank Server online no DB.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 7011, 0));

                using (var p = new PangyaBinaryWriter(0xA2))
                {
                    p.WritePStr(sl[0].ip);
                    p.WriteInt32(sl[0].port);
                    packet_func.session_send(p, _session);
                }


            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[GameServer.sendRankServer][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));

                using (var p = new PangyaBinaryWriter(0xA2))
                {
                    // Erro manda tudo 0
                    p.WriteUInt16(0);  // String IP
                    p.WriteUInt32(0);  // Port
                    packet_func.session_send(p, _session);
                }
            }
        }

        public virtual Channel findChannel(byte _channel)
        {
            if (_channel == 255)
                return null;

            for (var i = 0; i < v_channel.Count; ++i)
                if (v_channel[i].getId() == _channel)
                    return v_channel[i];

            return null;
        }

        public virtual Player findPlayer(uint _uid, bool _oid = false)
        {
            return (Player)(_oid ? FindSessionByOid(_uid) : FindSessionByUid(_uid));
        }

        public virtual void blockOID(uint _oid) { m_player_manager.blockOID(_oid); }
        public virtual void unblockOID(uint _oid) { m_player_manager.unblockOID(_oid); }

        DailyQuestInfo getDailyQuestInfo() { return m_dqi; }

 
        // Update Daily Quest Info
        public virtual void updateDailyQuest(DailyQuestInfo _dqi) { }

        // send Update Room Info, find room nos canais e atualiza o info
        public virtual void sendUpdateRoomInfo(room _r, int _option)
        {
            try
            {

                if (_r != null)
                {

                    var c = findChannel(_r.getChannelOwenerId());

                    if (c != null)
                        c.sendUpdateRoomInfo((RoomInfoEx)(_r.getInfo()), _option);
                }

            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[game_server::sendUpdateRoomInfo][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public virtual bool checkCommand(string[] _command) 
        
        {
            return true;
        }
        public virtual void reload_files() { }
        public virtual void init_systems()
        {
            // SINCRONAR por que se não alguem pode pegar lixo de memória se ele ainda nao estiver inicializado
            var cmd_dqi = new Cmd.CmdDailyQuestInfo();

            NormalManagerDB.add(1, cmd_dqi, SQLDBResponse, this);

            if (cmd_dqi.getException().getCodeError() != 0)
                throw new exception("[game_server::game_server][Error] nao conseguiu pegar o Daily Quest Info[Exption: "
                    + cmd_dqi.getException().getFullMessageError() + "]", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 277, 0));

            // Initialize Daily Quest of Server
            m_dqi = cmd_dqi.getInfo();


            // Para previnir de da exception no destructor do versus::treasure system
            var lixo = sRandomGen.getInstance().IsGood();

            // Carrega IFF_STRUCT
            if (!sIff.getInstance().isLoad())
                sIff.getInstance().load();


            //// Carrega Map Dados Estáticos
            if (!sMap.getInstance().isLoad())
                sMap.getInstance().load();

            //// Carrega Card System
            if (!sCardSystem.getInstance().isLoad())
                sCardSystem.getInstance().load();

            //// Carrega Comet Refill System
            if (!sCometRefillSystem.getInstance().isLoad())
                sCometRefillSystem.getInstance().load();

            // Carrega Papel Shop System
            if (!sPapelShopSystem.getInstance().isLoad())
                sPapelShopSystem.getInstance().load();

            //// Carrega Box System
            //if (!sBoxSystem.getInstance().isLoad())
            //    sBoxSystem.getInstance().load();

            //// Carrega Memorial System
            if (!sMemorialSystem.getInstance().isLoad())
                sMemorialSystem.getInstance().load();

            //// Carrega Cube Coin System
            if (!sCubeCoinSystem.getInstance().isLoad())
                sCubeCoinSystem.getInstance().load();

            //// Carrega Treasure Hunter System
            if (!sTreasureHunterSystem.getInstance().isLoad())
                sTreasureHunterSystem.getInstance().load();

            //// Carrega Drop System
            if (!sDropSystem.getInstance().isLoad())
                sDropSystem.getInstance().load();

            // Carrega Attendance Reward System
            if (!sAttendanceRewardSystem.getInstance().isLoad())
                sAttendanceRewardSystem.getInstance().load();

            //// Carrega Approach Mission System
            //if (!sApproachMissionSystem.getInstance().isLoad())
            //    sApproachMissionSystem.getInstance().load();

            //// Carrega Grand Zodiac Event System
            //if (!sGrandZodiacEvent.getInstance().isLoad())
            //    sGrandZodiacEvent.getInstance().load();

            //// Carrega Coin Cube Location Update Syatem
            //if (!sCoinCubeLocationUpdateSystem.getInstance().isLoad())
            //    sCoinCubeLocationUpdateSystem.getInstance().load();

            //// Carrega Golden Time System
            //if (!sGoldenTimeSystem.getInstance().isLoad())
            //    sGoldenTimeSystem.getInstance().load();

            //// Carrega Login Reward System
            //if (!sLoginRewardSystem.getInstance().isLoad())
            //    sLoginRewardSystem.getInstance().load();

            //// Carrega Bot GM Event
            if (!sBotGMEvent.getInstance().isLoad())
                sBotGMEvent.getInstance().load();

            //// Coloca aqui para ele não dá erro na hora de destruir o Room Grand Prix static instance
            //RoomGrandPrix::initFirstInstance();

            //// Coloca aqui para ele não dá erro na hora de destruir o Room Grand Zodiac Event static instance
            //RoomGrandZodiacEvent::initFirstInstance();

            //// Coloca aqui para ele não dá erro na hora de destruir o Room Bot GM Event static instance
            RoomBotGMEvent.initFirstInstance();
        }
        public virtual void init_Packets()
        {
            packet_func.funcs.addPacketCall(0x02, packet_func.packet002, this);
            packet_func.funcs.addPacketCall(0x03, packet_func.packet003, this);
            packet_func.funcs.addPacketCall(0x04, packet_func.packet004, this);
            packet_func.funcs.addPacketCall(0x06, packet_func.packet006, this);
            packet_func.funcs.addPacketCall(0x07, packet_func.packet007, this);
            packet_func.funcs.addPacketCall(0x08, packet_func.packet008, this);
            packet_func.funcs.addPacketCall(0x09, packet_func.packet009, this);
            packet_func.funcs.addPacketCall(0x0A, packet_func.packet00A, this);
            packet_func.funcs.addPacketCall(0x0B, packet_func.packet00B, this);
            packet_func.funcs.addPacketCall(0x0C, packet_func.packet00C, this);
            packet_func.funcs.addPacketCall(0x0D, packet_func.packet00D, this);
            packet_func.funcs.addPacketCall(0x0E, packet_func.packet00E, this);
            packet_func.funcs.addPacketCall(0x0F, packet_func.packet00F, this);
            packet_func.funcs.addPacketCall(0x10, packet_func.packet010, this);
            packet_func.funcs.addPacketCall(0x11, packet_func.packet011, this);
            packet_func.funcs.addPacketCall(0x12, packet_func.packet012, this);
            packet_func.funcs.addPacketCall(0x13, packet_func.packet013, this);
            packet_func.funcs.addPacketCall(0x14, packet_func.packet014, this);
            packet_func.funcs.addPacketCall(0x15, packet_func.packet015, this);
            packet_func.funcs.addPacketCall(0x16, packet_func.packet016, this);
            packet_func.funcs.addPacketCall(0x17, packet_func.packet017, this);
            packet_func.funcs.addPacketCall(0x18, packet_func.packet018, this);
            packet_func.funcs.addPacketCall(0x19, packet_func.packet019, this);
            packet_func.funcs.addPacketCall(0x1A, packet_func.packet01A, this);
            packet_func.funcs.addPacketCall(0x1B, packet_func.packet01B, this);
            packet_func.funcs.addPacketCall(0x1C, packet_func.packet01C, this);
            packet_func.funcs.addPacketCall(0x1D, packet_func.packet01D, this);
            packet_func.funcs.addPacketCall(0x1F, packet_func.packet01F, this);
            packet_func.funcs.addPacketCall(0x20, packet_func.packet020, this);
            packet_func.funcs.addPacketCall(0x22, packet_func.packet022, this);
            packet_func.funcs.addPacketCall(0x26, packet_func.packet026, this);
            packet_func.funcs.addPacketCall(0x29, packet_func.packet029, this);
            packet_func.funcs.addPacketCall(0x2A, packet_func.packet02A, this);
            packet_func.funcs.addPacketCall(0x2D, packet_func.packet02D, this);
            packet_func.funcs.addPacketCall(0x2F, packet_func.packet02F, this);
            packet_func.funcs.addPacketCall(0x30, packet_func.packet030, this);
            packet_func.funcs.addPacketCall(0x31, packet_func.packet031, this);
            packet_func.funcs.addPacketCall(0x32, packet_func.packet032, this);
            packet_func.funcs.addPacketCall(0x33, packet_func.packet033, this);
            packet_func.funcs.addPacketCall(0x34, packet_func.packet034, this);
            packet_func.funcs.addPacketCall(0x35, packet_func.packet035, this);
            packet_func.funcs.addPacketCall(0x36, packet_func.packet036, this);
            packet_func.funcs.addPacketCall(0x37, packet_func.packet037, this);
            packet_func.funcs.addPacketCall(0x39, packet_func.packet039, this);
            packet_func.funcs.addPacketCall(0x3A, packet_func.packet03A, this);
            packet_func.funcs.addPacketCall(0x3C, packet_func.packet03C, this);
            packet_func.funcs.addPacketCall(0x3D, packet_func.packet03D, this);
            packet_func.funcs.addPacketCall(0x3E, packet_func.packet03E, this);
            packet_func.funcs.addPacketCall(0x41, packet_func.packet041, this);
            packet_func.funcs.addPacketCall(0x42, packet_func.packet042, this);
            packet_func.funcs.addPacketCall(0x43, packet_func.packet043, this);
            packet_func.funcs.addPacketCall(0x47, packet_func.packet047, this);
            packet_func.funcs.addPacketCall(0x48, packet_func.packet048, this);
            packet_func.funcs.addPacketCall(0x4A, packet_func.packet04A, this);
            packet_func.funcs.addPacketCall(0x4B, packet_func.packet04B, this);
            packet_func.funcs.addPacketCall(0x4F, packet_func.packet04F, this);
            packet_func.funcs.addPacketCall(0x54, packet_func.packet054, this);
            packet_func.funcs.addPacketCall(0x55, packet_func.packet055, this);
            packet_func.funcs.addPacketCall(0x57, packet_func.packet057, this);
            packet_func.funcs.addPacketCall(0x5C, packet_func.packet05C, this);
            packet_func.funcs.addPacketCall(0x60, packet_func.packet060, this);
            packet_func.funcs.addPacketCall(0x61, packet_func.packet061, this);
            packet_func.funcs.addPacketCall(0x63, packet_func.packet063, this);
            packet_func.funcs.addPacketCall(0x64, packet_func.packet064, this);
            packet_func.funcs.addPacketCall(0x65, packet_func.packet065, this);
            packet_func.funcs.addPacketCall(0x66, packet_func.packet066, this);
            packet_func.funcs.addPacketCall(0x67, packet_func.packet067, this);
            packet_func.funcs.addPacketCall(0x69, packet_func.packet069, this);
            packet_func.funcs.addPacketCall(0x6B, packet_func.packet06B, this);
            packet_func.funcs.addPacketCall(0x73, packet_func.packet073, this);
            packet_func.funcs.addPacketCall(0x74, packet_func.packet074, this);
            packet_func.funcs.addPacketCall(0x75, packet_func.packet075, this);
            packet_func.funcs.addPacketCall(0x76, packet_func.packet076, this);
            packet_func.funcs.addPacketCall(0x77, packet_func.packet077, this);
            packet_func.funcs.addPacketCall(0x78, packet_func.packet078, this);
            packet_func.funcs.addPacketCall(0x79, packet_func.packet079, this);
            packet_func.funcs.addPacketCall(0x7A, packet_func.packet07A, this);
            packet_func.funcs.addPacketCall(0x7B, packet_func.packet07B, this);
            packet_func.funcs.addPacketCall(0x7C, packet_func.packet07C, this);
            packet_func.funcs.addPacketCall(0x7D, packet_func.packet07D, this);
            packet_func.funcs.addPacketCall(0x81, packet_func.packet081, this);
            packet_func.funcs.addPacketCall(0x82, packet_func.packet082, this);
            packet_func.funcs.addPacketCall(0x83, packet_func.packet083, this);
            packet_func.funcs.addPacketCall(0x88, packet_func.packet088, this);
            packet_func.funcs.addPacketCall(0x8B, packet_func.packet08B, this);
            packet_func.funcs.addPacketCall(0x8F, packet_func.packet08F, this);
            packet_func.funcs.addPacketCall(0x98, packet_func.packet098, this);
            packet_func.funcs.addPacketCall(0x9C, packet_func.packet09C, this);
            packet_func.funcs.addPacketCall(0x9D, packet_func.packet09D, this);
            packet_func.funcs.addPacketCall(0x9E, packet_func.packet09E, this);
            packet_func.funcs.addPacketCall(0xA1, packet_func.packet0A1, this);
            packet_func.funcs.addPacketCall(0xA2, packet_func.packet0A2, this);
            packet_func.funcs.addPacketCall(0xAA, packet_func.packet0AA, this);
            packet_func.funcs.addPacketCall(0xAB, packet_func.packet0AB, this);
            packet_func.funcs.addPacketCall(0xAE, packet_func.packet0AE, this);
            packet_func.funcs.addPacketCall(0xB2, packet_func.packet0B2, this);
            // Recebi esse pacote quando troquei de server, e no outro eu tinha jogado um Match feito bastante Achievement
            // e pegado daily quest, desistido do resto e aceito a do dia e aberto alguns card packs, ai troquei de server e recebi esse pacote
            //2018-11-17 20:43:07.307 Tipo : 180(0xB4), desconhecido ou nao implementado.func_arr.getPacketCall()     Error Code : 335609856
            //2018-11-17 20:43:07.307 size packet : 5
            //0000 B4 00 01 00 00 -- -- -- -- -- -- -- -- -- -- --    ................
            packet_func.funcs.addPacketCall(0xB4, packet_func.packet0B4, this);
            packet_func.funcs.addPacketCall(0xB5, packet_func.packet0B5, this);
            packet_func.funcs.addPacketCall(0xB7, packet_func.packet0B7, this);
            packet_func.funcs.addPacketCall(0xB9, packet_func.packet0B9, this);
            packet_func.funcs.addPacketCall(0xBA, packet_func.packet0BA, this);
            packet_func.funcs.addPacketCall(0xBD, packet_func.packet0BD, this);
            packet_func.funcs.addPacketCall(0xC1, packet_func.packet0C1, this);
            packet_func.funcs.addPacketCall(0xC9, packet_func.packet0C9, this);
            packet_func.funcs.addPacketCall(0xCA, packet_func.packet0CA, this);
            packet_func.funcs.addPacketCall(0xCB, packet_func.packet0CB, this);
            packet_func.funcs.addPacketCall(0xCC, packet_func.packet0CC, this);
            packet_func.funcs.addPacketCall(0xCD, packet_func.packet0CD, this);
            packet_func.funcs.addPacketCall(0xCE, packet_func.packet0CE, this);
            packet_func.funcs.addPacketCall(0xCF, packet_func.packet0CF, this);
            packet_func.funcs.addPacketCall(0xD0, packet_func.packet0D0, this);
            packet_func.funcs.addPacketCall(0xD1, packet_func.packet0D1, this);
            packet_func.funcs.addPacketCall(0xD2, packet_func.packet0D2, this);
            packet_func.funcs.addPacketCall(0xD3, packet_func.packet0D3, this);
            packet_func.funcs.addPacketCall(0xD4, packet_func.packet0D4, this);
            packet_func.funcs.addPacketCall(0xD5, packet_func.packet0D5, this);
            packet_func.funcs.addPacketCall(0xD8, packet_func.packet0D8, this);
            packet_func.funcs.addPacketCall(0xDE, packet_func.packet0DE, this);
            packet_func.funcs.addPacketCall(0xE5, packet_func.packet0E5, this);
            packet_func.funcs.addPacketCall(0xE6, packet_func.packet0E6, this);
            packet_func.funcs.addPacketCall(0xE7, packet_func.packet0E7, this);
            packet_func.funcs.addPacketCall(0xEB, packet_func.packet0EB, this);
            packet_func.funcs.addPacketCall(0xEC, packet_func.packet0EC, this);
            packet_func.funcs.addPacketCall(0xEF, packet_func.packet0EF, this);
            packet_func.funcs.addPacketCall(0xF4, packet_func.packet0F4, this);
            packet_func.funcs.addPacketCall(0xFB, packet_func.packet0FB, this);
            packet_func.funcs.addPacketCall(0xFE, packet_func.packet0FE, this);
            packet_func.funcs.addPacketCall(0x119, packet_func.packet119, this);
            packet_func.funcs.addPacketCall(0x126, packet_func.packet126, this);
            packet_func.funcs.addPacketCall(0x127, packet_func.packet127, this);
            packet_func.funcs.addPacketCall(0x128, packet_func.packet128, this);
            packet_func.funcs.addPacketCall(0x129, packet_func.packet129, this);
            packet_func.funcs.addPacketCall(0x12C, packet_func.packet12C, this);
            packet_func.funcs.addPacketCall(0x12D, packet_func.packet12D, this);
            packet_func.funcs.addPacketCall(0x12E, packet_func.packet12E, this);
            packet_func.funcs.addPacketCall(0x12F, packet_func.packet12F, this);
            packet_func.funcs.addPacketCall(0x130, packet_func.packet130, this);
            packet_func.funcs.addPacketCall(0x131, packet_func.packet131, this);
            packet_func.funcs.addPacketCall(0x137, packet_func.packet137, this);
            packet_func.funcs.addPacketCall(0x138, packet_func.packet138, this);
            packet_func.funcs.addPacketCall(0x140, packet_func.packet140, this);
            packet_func.funcs.addPacketCall(0x141, packet_func.packet141, this);
            packet_func.funcs.addPacketCall(0x143, packet_func.packet143, this);
            packet_func.funcs.addPacketCall(0x144, packet_func.packet144, this);
            packet_func.funcs.addPacketCall(0x145, packet_func.packet145, this);
            packet_func.funcs.addPacketCall(0x146, packet_func.packet146, this);
            packet_func.funcs.addPacketCall(0x147, packet_func.packet147, this);
            packet_func.funcs.addPacketCall(0x14B, packet_func.packet14B, this);
            packet_func.funcs.addPacketCall(0x151, packet_func.packet151, this);
            packet_func.funcs.addPacketCall(0x152, packet_func.packet152, this);
            packet_func.funcs.addPacketCall(0x153, packet_func.packet153, this);
            packet_func.funcs.addPacketCall(0x154, packet_func.packet154, this);
            packet_func.funcs.addPacketCall(0x155, packet_func.packet155, this);
            packet_func.funcs.addPacketCall(0x156, packet_func.packet156, this);
            packet_func.funcs.addPacketCall(0x157, packet_func.packet157, this);
            packet_func.funcs.addPacketCall(0x158, packet_func.packet158, this);
            packet_func.funcs.addPacketCall(0x15C, packet_func.packet15C, this);
            packet_func.funcs.addPacketCall(0x15D, packet_func.packet15D, this);
            packet_func.funcs.addPacketCall(0x164, packet_func.packet164, this);
            packet_func.funcs.addPacketCall(0x165, packet_func.packet165, this);
            packet_func.funcs.addPacketCall(0x166, packet_func.packet166, this);
            packet_func.funcs.addPacketCall(0x167, packet_func.packet167, this);
            packet_func.funcs.addPacketCall(0x168, packet_func.packet168, this);
            packet_func.funcs.addPacketCall(0x169, packet_func.packet169, this);
            packet_func.funcs.addPacketCall(0x16B, packet_func.packet16B, this);
            packet_func.funcs.addPacketCall(0x16C, packet_func.packet16C, this);
            packet_func.funcs.addPacketCall(0x16D, packet_func.packet16D, this);
            packet_func.funcs.addPacketCall(0x16E, packet_func.packet16E, this);
            packet_func.funcs.addPacketCall(0x16F, packet_func.packet16F, this);
            packet_func.funcs.addPacketCall(0x171, packet_func.packet171, this);
            packet_func.funcs.addPacketCall(0x172, packet_func.packet172, this);
            packet_func.funcs.addPacketCall(0x176, packet_func.packet176, this);
            packet_func.funcs.addPacketCall(0x177, packet_func.packet177, this);
            packet_func.funcs.addPacketCall(0x179, packet_func.packet179, this);
            packet_func.funcs.addPacketCall(0x17A, packet_func.packet17A, this);
            packet_func.funcs.addPacketCall(0x17F, packet_func.packet17F, this);
            packet_func.funcs.addPacketCall(0x180, packet_func.packet180, this);
            packet_func.funcs.addPacketCall(0x181, packet_func.packet181, this);
            packet_func.funcs.addPacketCall(0x184, packet_func.packet184, this);
            packet_func.funcs.addPacketCall(0x185, packet_func.packet185, this);
            packet_func.funcs.addPacketCall(0x187, packet_func.packet187, this);
            packet_func.funcs.addPacketCall(0x188, packet_func.packet188, this);
            packet_func.funcs.addPacketCall(0x189, packet_func.packet189, this);
            packet_func.funcs.addPacketCall(0x18A, packet_func.packet18A, this);
            packet_func.funcs.addPacketCall(0x18B, packet_func.packet18B, this);
            packet_func.funcs.addPacketCall(0x18C, packet_func.packet18C, this);
            packet_func.funcs.addPacketCall(0x18D, packet_func.packet18D, this);
            packet_func.funcs.addPacketCall(0x192, packet_func.packet192, this);
            packet_func.funcs.addPacketCall(0x196, packet_func.packet196, this);
            packet_func.funcs.addPacketCall(0x197, packet_func.packet197, this);
            packet_func.funcs.addPacketCall(0x198, packet_func.packet198, this);
            packet_func.funcs.addPacketCall(0x199, packet_func.packet199, this);

            packet_func.funcs_sv.addPacketCall(0x3F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x40, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x42, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x44, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x45, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x46, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x47, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x48, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x49, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x4A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x4B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x4C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x4D, packet_func.packet_sv4D, this);
            packet_func.funcs_sv.addPacketCall(0x4E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x50, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x52, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x53, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x55, packet_func.packet_sv055, this);
            packet_func.funcs_sv.addPacketCall(0x56, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x58, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x59, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x5A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x5B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x5C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x5D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x60, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x61, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x63, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x64, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x65, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x66, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x67, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x68, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x6A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x6B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x6C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x6D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x6E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x70, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x71, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x72, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x73, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x76, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x77, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x78, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x79, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x7C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x7D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x7E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x83, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x84, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x86, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x89, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x8A, packet_func.packet_sv055, this);   // Esse pede o pacote 0x1B de tacada de novo do player que está com lag
            packet_func.funcs_sv.addPacketCall(0x8B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x8C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x8D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x8E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x90, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x91, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x92, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x93, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x94, packet_func.packet_svFazNada, this);   // Resposta player report chat game
            packet_func.funcs_sv.addPacketCall(0x95, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x96, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x97, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x9A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x9E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x9F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA1, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA2, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA3, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA5, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xA7, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xAA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xAC, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xB0, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xB2, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xB4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xB9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xBA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xBF, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC2, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC5, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC7, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC8, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xC9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xCA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xCC, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xCE, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xD4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xD7, packet_func.packet_svFazNada, this);   // Request GameGuard Auth
            packet_func.funcs_sv.addPacketCall(0xE1, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE2, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE3, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE5, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE6, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE7, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE8, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xE9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xEA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xEB, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xEC, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xED, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xF1, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xF5, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xF6, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xF8, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xF9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xFA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xFB, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0xFC, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x101, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x102, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x10B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x10E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x10F, packet_func.packet_svFazNada, this);  // Dialog Level Up!
            packet_func.funcs_sv.addPacketCall(0x113, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x115, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x11A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x11B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x11C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x11F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x129, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x12A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x12B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x12D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x12E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x12F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x130, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x131, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x132, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x133, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x134, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x135, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x136, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x137, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x138, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x139, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x13F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x144, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x14E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x14F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x150, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x151, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x153, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x154, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x156, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x157, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x158, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x159, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x15A, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x15B, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x15C, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x15D, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x15E, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x160, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x168, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x169, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x16A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x16C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x16D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x16E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x16F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x170, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x171, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x172, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x173, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x174, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x176, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x181, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x18D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x18F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x190, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x196, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x197, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x198, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x199, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x19D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1A9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1AD, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1B1, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1D3, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1D4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1D9, packet_func.packet_svFazNada, this);  // Update ON GAME. Level And Exp
            packet_func.funcs_sv.addPacketCall(0x1E7, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1E8, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1E9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1EA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1EC, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1EE, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1EF, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F0, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F1, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F2, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F3, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F4, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F5, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F7, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F8, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1F9, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x1FA, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x200, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x201, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x203, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x20E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x210, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x211, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x212, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x213, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x214, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x215, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x216, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x21B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x21D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x21E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x220, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x225, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x226, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x227, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x228, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x229, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x22A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x22B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x22C, packet_func.packet_svFazNada/*packet_sv22D*/, this);
            packet_func.funcs_sv.addPacketCall(0x22D, packet_func.packet_svFazNada/*packet_sv22D*/, this);
            packet_func.funcs_sv.addPacketCall(0x22E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x22F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x230, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x231, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x236, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x237, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x23D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x23E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x23F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x240, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x241, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x242, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x243, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x244, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x245, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x246, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x247, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x248, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x249, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x24C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x24F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x250, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x251, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x253, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x254, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x255, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x256, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x257, packet_func.packet_svRequestInfo, this);
            packet_func.funcs_sv.addPacketCall(0x258, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x259, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x25A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x25C, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x25D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x264, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x265, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x266, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x26A, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x26B, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x26D, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x26E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x26F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x270, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x271, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x272, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x273, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x274, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x27E, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x27F, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x280, packet_func.packet_svFazNada, this);
            packet_func.funcs_sv.addPacketCall(0x281, packet_func.packet_svFazNada, this);

            // Auth Server Comandos
            packet_func.funcs_as.addPacketCall(0x1, packet_func.packet_as001, this);
        }
        public virtual void init_load_channels()
        {
            v_channel = new List<Channel>();
            try
            {
                int num_channel = m_reader_ini.readInt("CHANNELINFO", "NUM_CHANNEL");

                for (byte i = 0; i < num_channel; ++i)
                {
                    ChannelInfo ci = new ChannelInfo();
                    try
                    {
                        ci.id = i;
                        ci.name = m_reader_ini.ReadString("CHANNEL" + (i + 1), "NAME");
                        ci.max_user = m_reader_ini.ReadInt16("CHANNEL" + (i + 1), "MAXUSER");
                        ci.min_level_allow = m_reader_ini.ReadUInt32("CHANNEL" + (i + 1), "LOWLEVEL");
                        ci.max_level_allow = m_reader_ini.ReadUInt32("CHANNEL" + (i + 1), "MAXLEVEL");
                        ci.flag.ulFlag = m_reader_ini.ReadUInt32("CHANNEL" + (i + 1), "FLAG");
                    }
                    catch (Exception e)
                    {
                        _smp.message_pool.push("[GameServer.init_load_channels][ErrorSystem] " + e.Message);
                    }

                    v_channel.Add(new Channel(ci, m_si.propriedade));
                }
            }
            catch (Exception e)
            {
                _smp.message_pool.push("[GameServer.init_load_channels][ErrorSystem] " + e.Message);
            }

        }
        public virtual void reload_systems() { }
        public virtual void reloadGlobalSystem(uint _tipo) { }

        // Update rate e Event of Server
        public virtual void updaterateAndEvent(uint _tipo, uint _qntd) { }

        // Shutdown With Time


        // Check Player Itens

        public virtual void check_player()
        {
            _smp::message_pool.push(new message("[game_server::check_player][Log] check_player iniciado com sucesso!"));

            while (true)
            {
                if (m_state != ServerState.Initialized)
                    continue;

                // Verifica Game Guard Auth do player
                //if (m_GameGuardAuth)
                //    m_player_manager.checkPlayersGameGuard();

                // Verifica se os itens dos players está tudo normal
                m_player_manager.checkPlayersItens();
                //vai dormir por 10000 milessimos
                Thread.Sleep(10000);
            }
        }

        // Make Grand Zodiac Event Room
        public virtual void makeGrandZodiacEventRoom() { }

        // Make List of Players to Golden Time Event
        public virtual void makeListOfPlayersToGoldenTime() { }

        // Make Bot GM Event Room
        public void makeBotGMEventRoom()
        {
            // Lambda getItemName
            Func<uint, string> getItemName = (_typeid) =>
            {
                string ret = "";

                var @base = sIff.getInstance().findCommomItem(_typeid);

                if (@base != null)
                {
                    ret = (@base.Name);
                }

                return ret;
            };

            try
            {
                var rt = sBotGMEvent.getInstance().getInterval();
                List<stReward> reward = new List<stReward>();

                if (rt != null && !rt.m_sended_message)
                {

                    sBotGMEvent.getInstance().setSendedMessage();
                    rt.m_sended_message = true;

                    int duration_event_interval = 2;

                    reward = sBotGMEvent.getInstance().calculeReward();

                    string reward_str = "{";
                    for (int i = 0; i < reward.Count; i++)
                    {
                        if (i > 0) reward_str += ", [";
                        else reward_str += "[";

                        reward_str += reward[i].toString() + "]";
                    }
                    reward_str += "}";

                    message_pool.push(new message(
                        "[game_server::makeBotGMEventRoom][Log] Bot GM Event comecou, criando a sala no canal[ID=" +
                        rt.m_channel_id + "], o jogo comeca em " + duration_event_interval +
                        " minutos.",
                        type_msg.CL_FILE_LOG_AND_CONSOLE));

                    // Mensagem que será broadcastada
                    string channelName = "Canal (Livre 1)";
                    var c = findChannel(rt.m_channel_id);
                    if (c != null)
                    {
                        channelName = c.getInfo().name;
                    }

                    string premios = "";
                    for (int i = 0; i < reward.Count; i++)
                    {
                        if (i > 0) premios += ", ";

                        var r = reward[i];
                        string quantidade = (r.qntd_time > 0)
                            ? r.qntd_time + "day"
                            : r.qntd.ToString();

                        premios += getItemName(r._typeid) + "(" + quantidade + ")";
                    }

                    string msg = MESSAGE_BOT_GM_EVENT_START_PART1 +
                                 channelName +
                                 MESSAGE_BOT_GM_EVENT_START_PART2 +
                                 duration_event_interval +
                                 MESSAGE_BOT_GM_EVENT_START_PART3 +
                                 premios;

                    var p = new PangyaBinaryWriter(0x1D3);
                    p.WriteUInt32(2u);

                    for (uint i = 0; i < 2; ++i)
                    {
                        p.WriteUInt32(eBROADCAST_TYPES.BT_MESSAGE_PLAIN);
                        p.WriteString(msg);
                    }

                    foreach (var ch in v_channel)
                    {
                        packet_func.channel_broadcast(ch, p, 1);
                    }
                }

                if (rt != null)
                {
                    var c = findChannel(rt.m_channel_id);
                    if (c != null)
                    {
                        c.makeBotGMEventRoom(rt, reward);
                    }
                }
                else
                {
                    message_pool.push(new message(
                        "[game_server::makeBotGMEventRoom][WARNING] Entrou no makeBotGMEventRoom, mas nao tem nenhum stRangeTime(interval) que o Bot GM Event esta ativado.",
                        type_msg.CL_FILE_LOG_AND_CONSOLE));
                }
            }
            catch (exception e)
            {
                message_pool.push(new message(
                    "[game_server::makeBotGMEventRoom][ErrorSystem] " + e.getFullMessageError(),
                    type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }


        protected override void onAcceptCompleted(PangyaAPI.Network.PangyaSession.Session _session)
        {
            try
            {

                packet _packet = new packet(0x3F);
                _packet.AddByte(1); // OPTION 1
                _packet.AddByte(1); // OPTION 2
                _packet.AddByte(_session.m_key);	// Key
                _packet.makeRaw();

                var mb = _packet.getBuffer();
                _session.requestSendBuffer(mb, true);
            }
            catch (exception ex)
            {
                message_pool.push(new message(
              $"[GameServer.onAcceptCompleted][ErrorSt]: {ex.getFullMessageError()}",
              type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public override bool CheckPacket(PangyaAPI.Network.PangyaSession.Session _session, packet packet, int opt = 0)
        {
            var player = (Player)_session;
            var packetId = packet.Id;


            switch (opt)
            {
                case 1:
                    // Verifica se o valor de packetId é válido no enum PacketIDClient
                    if (Enum.IsDefined(typeof(PacketIDClient), (PacketIDClient)packetId))
                    {
                        if ((PacketIDClient)packetId != PacketIDClient.CLIENT_HEARTBEAT_0xF4)
                            Debug.WriteLine($"[GameServer.CheckPacket][Log]: PLAYER[UID: {player.m_pi.uid}, CGPID: {(PacketIDClient)packetId}]", ConsoleColor.Cyan);
                        return true;
                    }
                    else// nao tem no PacketIDClient
                    {
                        Debug.WriteLine($"[GameServer.CheckPacket][Log]: PLAYER[UID: {player.m_pi.uid}, CGPID: 0x{packet.Id:X}]");
                        return true;
                    }
                default:
                    // Verifica se o valor de packetId é válido no enum PacketIDServer
                    if (Enum.IsDefined(typeof(PacketIDServer), (PacketIDServer)packetId))
                    {
                        Debug.WriteLine($"[GameServer.CheckPacket][Log]: PLAYER[UID: {player.m_pi.uid}, SGPID: {(PacketIDServer)packetId}]", ConsoleColor.Cyan);
                        return true;
                    }
                    else// nao tem no PacketIDServer
                    {
                        Debug.WriteLine($"[GameServer.CheckPacket][Log]: PLAYER[UID: {player.m_pi.uid}, SGPID: 0x{packet.Id:X}]");
                        return true;
                    }
            }
        }


        public override void onDisconnected(PangyaAPI.Network.PangyaSession.Session _session)
        {
            if (_session == null)
                throw new exception("[GameServer.onDisconnected][Error] _session is null");

            var _player = (Player)_session;

            _smp.message_pool.push(new message("[GameServer.onDisconnected][Log] Player Desconectou. ID: " + _player.m_pi.id + "  UID: " + _player.m_pi.uid));

            /// Novo
            var _channel = findChannel(_player.m_pi.channel);

            try
            {

                if (_channel != null)
                    _channel.leaveChannel(_player);

            }
            catch (exception e)
            {
                _smp.message_pool.push(new message("[GameServer.onDisconnect][Error] " + e.getFullMessageError()));
            }
        }

        //chama alguma coisa aqui!
        public override void OnStart()
        {
            Console.Title = $"Game Server - P: {m_si.curr_user}";
        }

        public Channel enterChannel(Player _session, byte _channel)
        {
            Channel enter = null, last = null;
            var p = new PangyaBinaryWriter();
            try
            {

                if ((enter = findChannel(_channel)) == null)
                    throw new Exception("[GameServer::enterChannel][Error] id channel nao exite.");

                if (enter.getId() == _session.m_pi.channel)
                {
                    packet_func.session_send(packet_func.pacote04E(1), _session);
                    return enter;   // Ele já está nesse canal
                }

                if (enter.isFull())
                {
                    // Não conseguiu entrar no canal por que ele está cheio, deixa o enter como null
                    enter = null;
                    packet_func.session_send(packet_func.pacote04E(2), _session);
                }
                else
                {
                    // Verifica se pode entrar no canal
                    enter.checkEnterChannel(_session);

                    // Sai do canal antigo se ele estiver em outro canal
                    if (_session.m_pi.channel != DEFAULT_CHANNEL && (last = findChannel(_session.m_pi.channel)) != null)
                        last.leaveChannel(_session);

                    // Entra no canal
                    enter.enterChannel(_session);
                }
            }
            catch (exception e)
            {
                _smp::message_pool.push(new message("[GameServer.enterChannel][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
                packet_func.session_send(packet_func.pacote04E(-1), _session);
            }

            return enter;
        }

        public void requestChangeChatMacroUser(Player _session, packet _packet)
        {
            try
            {

                // Verifica se session está autorizada para executar esse ação, 
                // se ele não fez o login com o Server ele não pode fazer nada até que ele faça o login

                chat_macro_user cmu;

                cmu = (chat_macro_user)_packet.Read(new chat_macro_user());

                // UPDATE ON GAME

                // Se vazio substitiu por um macro padrão
                for (var i = 0u; i < 9; ++i)
                    if (string.IsNullOrEmpty(cmu.macro[i]))
                        cmu.macro[i] = "PangYa! Por favor configure seu chat macro";

                _session.m_pi.cmu = cmu;

                // UPDATE ON DB
                NormalManagerDB.add(3, new Cmd.CmdUpdateChatMacroUser(_session.m_pi.uid, _session.m_pi.cmu), SQLDBResponse, this);

            }
            catch (exception e)
            {
                _smp::message_pool.push(new message("[GameServer.requestChangeChatMacroUser][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public void requestChangeServer(Player _session, packet _packet)
        {

            try
            {

                var server_uid = _packet.ReadUInt32();

                var it = m_server_list.FirstOrDefault(c => c.uid == server_uid);

                if (it == null)
                    throw new exception("[GameServer.requestChangeServer][Error] Player[UID=" + (_session.m_pi.uid)
                            + "] tentou trocar de server para o Server[UID=" + (server_uid)
                            + "], mas ele nao esta no server list mais.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 0x7500001, 1));

                if (_session.m_pi.lobby != DEFAULT_CHANNEL && _session.m_pi.lobby == 176u/*Grand Prix*/
                    && !it.propriedade.grand_prix/*Não é Grand Prix o Server*/)
                    throw new exception("[GameServer.requestChangeServer][Error] Player[UID=" + (_session.m_pi.uid)
                            + "] tentou trocar de server para o Server[UID=" + (server_uid)
                            + "], mas o player esta na lobby grand prix e o server que ele quer entrar nao e' grand prix.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 0x7500002, 2));

                var cmd_akg = new CmdAuthKeyGame(_session.m_pi.uid, server_uid);    // waitable

                NormalManagerDB.add(0, cmd_akg, null, null);

                if (cmd_akg.getException().getCodeError() != 0)
                    throw cmd_akg.getException();

                var auth_key_game = cmd_akg.getAuthKey();

                var cmd_uakl = new CmdUpdateAuthKeyLogin(_session.m_pi.uid, 1); // waitable

                NormalManagerDB.add(0, cmd_uakl, null, null);

                if (cmd_uakl.getException().getCodeError() != 0)
                    throw cmd_uakl.getException();

                packet_func.session_send(packet_func.pacote1D4(auth_key_game), _session);

            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[requestChangeServer][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));

                // Envia server lista novamente para o player ele foi proibido de entrar no server ou não conseguiu por algum motivo ou erro
                sendServerListAndChannelListToSession(_session);
            }
        }

        public void requestChangeWhisperState(Player _session, packet _packet)
        {
            try
            {

                var whisper = _packet.ReadByte();

                // Verifica se session está autorizada para executar esse ação, 
                // se ele não fez o login com o Server ele não pode fazer nada até que ele faça o login
                //CHECK_SESSION_IS_AUTHORIZED("ChangeWisperState");

                if (whisper > 1)
                    throw new exception("[GameServer.requestChangeWhisperState][Error] player[UID=" + (_session.m_pi.uid) + "] tentou alterar o estado do Whisper[state="
                            + ((ushort)whisper) + "], mas ele mandou um valor invalido. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 1, 0x5300101));

                _session.m_pi.mi.state_flag.whisper = (_session.m_pi.whisper = whisper) == 1 ? true : false;

                // Log
                _smp::message_pool.push(new message("[Whisper::ChangeState][Log] player[UID=" + (_session.m_pi.uid) + "] trocou o Whisper State para : " + (whisper.IsTrue() ? ("ON") : ("OFF")), type_msg.CL_FILE_LOG_AND_CONSOLE));


            }
            catch (exception e)
            {
                _smp::message_pool.push(new message("[GameServer.requestChangeWhisperState][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public void requestChat(Player _session, packet _packet)
        {
            try
            {

                string nickname = "", msg = "";

                nickname = _packet.ReadPStr();
                msg = _packet.ReadPStr();

                // Verifica a mensagem com palavras proibida e manda para o log e bloquea o chat dele
                _smp::message_pool.push(new message("[GameServer.requestChat][Log]: PLAYER[UID: " + _session.m_pi.uid + ", MSG: " + msg + "]", type_msg.CL_FILE_LOG_AND_CONSOLE));

                var c = findChannel(_session.m_pi.channel);

                if (c != null)
                {

                    // LOG GM
                    // Envia para todo os GM do server essa message
                    var gmList = FindAllGM();

                    if (gmList.Any())
                    {
                        string msg_gm = "\\5" + _session.m_pi.nickname + ": '" + msg + "'";
                        string from = "\\1[Channel=" + c.getInfo().name + ", \\1ROOM=" + _session.m_pi.mi.sala_numero + "]";

                        int index = from.IndexOf(' ');
                        if (index != -1)
                            from = from.Substring(0, index) + " \\1" + from.Substring(index + 1);

                        // Rotina normal de notificação para GM no chat global
                        foreach (Player el in gmList)
                        {
                            if (((el.m_gi.channel && el.m_pi.channel == c.getInfo().id) || el.m_gi.whisper.IsTrue() || el.m_gi.isOpenPlayerWhisper(_session.m_pi.uid))
                                && /* Check SAME Channel and Room*/(el.m_pi.channel != _session.m_pi.channel || el.m_pi.mi.sala_numero != _session.m_pi.mi.sala_numero))
                            {
                                // Responde no chat do player     
                                packet_func.session_send(packet_func.pacote040(from, msg_gm, 0), el);
                            }
                        }
                    }
                }

                // Normal Message
                if (_session.m_pi.mi.sala_numero != ushort.MaxValue)
                    c.requestSendMsgChatRoom(_session, msg);
                else
                {
                    //is low :/
                    packet_func.channel_broadcast(c, packet_func.pacote040(_session.m_pi.nickname, msg, (byte)(_session.m_pi.m_cap.game_master ? 128 : 0)), 1);

                }

            }
            catch (exception e)
            {
                Console.WriteLine(e.getFullMessageError());
            }
        }

        public void requestCheckGameGuardAuthAnswer(Player _session, packet _packet)
        {
        }

        public void requestCommandNoticeGM(Player _session, packet _packet)
        {
            try
            {

                if (!(_session.m_pi.m_cap.game_master/* & 4*/))
                    throw new exception("[GameServer.requestCommandNoticeGM][Error] player[UID=" + (_session.m_pi.uid)
                            + "] nao eh GM mas tentou executar comando GM. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 1, 0x5700100));

                string notice = _packet.ReadString();

                if (notice.empty())
                    throw new exception("[GameServer.requestCommandNoticeGM][Error] player[UID=" + (_session.m_pi.uid)
                            + "] tentou executar o comando de notice, mas a notice is empty. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 8, 0x5700100));

                // Log
                _smp::message_pool.push(new message("[GameServer.requestCommandNoticeGM][Log] player[UID=" + (_session.m_pi.uid) + "] enviou notice[NOTICE="
                        + notice + "] para todos do game server.", type_msg.CL_FILE_LOG_AND_CONSOLE));

                using (var p = new PangyaBinaryWriter(0x40))
                {
                    p.WriteByte(7); // Notice

                    p.WritePStr(_session.m_pi.nickname);
                    p.WritePStr(notice);
                    foreach (var c in v_channel)
                        packet_func.channel_broadcast(c, p.GetBytes);
                }
            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[GameServer.requestCommandNoticeGM][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
                using (var p = new PangyaBinaryWriter(0x40))
                {
                    p.WriteByte(7); // Notice

                    p.WritePStr(_session.m_pi.nickname);
                    p.WritePStr("Nao conseguiu executar o comando.");
                    packet_func.session_send(p, _session);

                }
            }

        }

        public void requestCommonCmdGM(Player _session, packet _packet)
        {
            try
            {
                _session.requestCommonCmdGM(_packet);
            }
            catch (exception e)
            {
                Console.WriteLine(e.getFullMessageError());
            }
        }

        public void requestEnterChannel(Player _session, packet _packet)
        {
            try
            {
                _packet.ReadByte(out byte channel);
                // Enter Channel
                enterChannel(_session, channel);

                //if (!sAttendanceRewardSystem.getInstance().isLoad())
                //    sAttendanceRewardSystem.getInstance().load();
                //var m_ari = _session.m_pi.ari;
                //var p = new PangyaBinaryWriter();
                //if (m_ari.login == 2 || m_ari.login == 3) //sem o now
                //{
                //    if (_session.m_pi.ari.counter == 0)
                //        _session.m_pi.ari.counter = 1;
                //    else
                //        _session.m_pi.ari.counter = _session.m_pi.ari.counter + 1;

                //    var reward_item = sAttendanceRewardSystem.getInstance().drawReward(1);

                //    if (reward_item == null)
                //        throw new exception("[AttendanceRewardSystem::requestCheckAttendance][Error] nao conseguiu sortear um item para o player. Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.ATTENDANCE_REWARD_SYSTEM, 7, 0));

                //    _session.m_pi.ari.now._typeid = reward_item._typeid;
                //    _session.m_pi.ari.now.qntd = reward_item.qntd;
                //    if (sIff.getInstance().IsExist(_session.m_pi.ari.now._typeid) == false)
                //    {
                //        //gera o proximo se não existir dados la na db
                //        reward_item = sAttendanceRewardSystem.getInstance().drawReward((byte)(((_session.m_pi.ari.counter + 1) % 10 == 0) ? 2 : 1)/*Item Normal*/);

                //        if (reward_item == null)
                //            throw new exception("[AttendanceRewardSystem::requestCheckAttendance][Error] nao conseguiu sortear um item para o player. Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.ATTENDANCE_REWARD_SYSTEM, 7, 0));

                //        _session.m_pi.ari.now._typeid = reward_item._typeid;
                //        _session.m_pi.ari.now.qntd = reward_item.qntd;
                //    }

                //    else if (sIff.getInstance().IsExist(_session.m_pi.ari.after._typeid) == false)
                //    {
                //        //gera o proximo se não existir dados la na db
                //        reward_item = sAttendanceRewardSystem.getInstance().drawReward((byte)(((_session.m_pi.ari.counter + 1) % 10 == 0) ? 2/*Tipo 2 Papel Box*/ : 1)/*Item Normal*/);

                //        if (reward_item == null)
                //            throw new exception("[AttendanceRewardSystem::requestCheckAttendance][Error] nao conseguiu sortear um item para o player. Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.ATTENDANCE_REWARD_SYSTEM, 7, 0));

                //        _session.m_pi.ari.after._typeid = reward_item._typeid;
                //        _session.m_pi.ari.after.qntd = reward_item.qntd;
                //    }
                //    _session.m_pi.ari.last_login.CreateTime();
                //    // Zera as Horas deixa s� a date
                //    _session.m_pi.ari.last_login.MilliSecond = _session.m_pi.ari.last_login.Second = _session.m_pi.ari.last_login.Minute = _session.m_pi.ari.last_login.Hour = 0;

                //    stItem item = new stItem();
                //    item.type = 2;
                //    item.id = -1;
                //    item._typeid = _session.m_pi.ari.now._typeid;
                //    item.qntd = _session.m_pi.ari.now.qntd;
                //    item.STDA_C_ITEM_QNTD = (ushort)(short)item.qntd;

                //    string msg = "Your Attendance rewards have arrived!";

                //    MailBoxManager.sendMessageWithItem(0, _session.m_pi.uid, msg, item);

                //    _session.m_pi.ari.login = 0; 
                //    packet_func.session_send(packet_func.pacote248(_session.m_pi.ari), _session, 0);
                //    _session.m_pi.ari.counter = 0;//vai pro zero de novo	  

                //    // D� 3 Grand Prix Ticket, por que � a primeira vez que o player loga no dia
                //    //sAttendanceRewardSystem.getInstance().sendGrandPrixTicket(_session);
                //    // D� 5 Key of fortune, por que � a primeira vez que o player loga no dia
                //    //sAttendanceRewardSystem.getInstance().sendFortuneKey(_session);

                //}
                //else
                //{
                //    if (sAttendanceRewardSystem.getInstance().passedOneDay(_session))
                //    {
                //        // Reward
                //        stItem item = new stItem();

                //        // Passou 1 dia depois que o player logou no pangya	  	
                //        _session.m_pi.ari.login = 0;
                //        _session.m_pi.ari.now = _session.m_pi.ari.after;
                //        // Troca o item after para now
                //        if (sIff.getInstance().IsExist(_session.m_pi.ari.now._typeid) == false)
                //        {
                //            //gera o proximo se não existir dados la na db
                //            var reward_item = sAttendanceRewardSystem.getInstance().drawReward((byte)(((_session.m_pi.ari.counter + 1) % 10 == 0) ? 2/*Tipo 2 Papel Box*/ : 1)/*Item Normal*/);

                //            if (reward_item == null)
                //                throw new exception("[AttendanceRewardSystem::requestCheckAttendance][Error] nao conseguiu sortear um item para o player. Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.ATTENDANCE_REWARD_SYSTEM, 7, 0));

                //            _session.m_pi.ari.now._typeid = reward_item._typeid;
                //            _session.m_pi.ari.now.qntd = reward_item.qntd;
                //        }

                //        else if (sIff.getInstance().IsExist(_session.m_pi.ari.after._typeid) == false)
                //        {
                //            //gera o proximo se não existir dados la na db
                //            var reward_item = sAttendanceRewardSystem.getInstance().drawReward((byte)(((_session.m_pi.ari.counter + 1) % 10 == 0) ? 2/*Tipo 2 Papel Box*/ : 1)/*Item Normal*/);

                //            if (reward_item == null)
                //                throw new exception("[AttendanceRewardSystem::requestCheckAttendance][Error] nao conseguiu sortear um item para o player. Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.ATTENDANCE_REWARD_SYSTEM, 7, 0));

                //            _session.m_pi.ari.after._typeid = reward_item._typeid;
                //            _session.m_pi.ari.after.qntd = reward_item.qntd;
                //        }
                //        // Limpa o After
                //        _session.m_pi.ari.after.clear();

                //        item.type = 2;
                //        item.id = -1;
                //        item._typeid = _session.m_pi.ari.now._typeid;
                //        item.qntd = _session.m_pi.ari.now.qntd;
                //        item.STDA_C_ITEM_QNTD = (ushort)item.qntd;

                //        var msg = "Your Attendance rewards have arrived!";

                //        MailBoxManager.sendMessageWithItem(0, _session.m_pi.uid, msg, item);
                //        _session.m_pi.ari.counter = _session.m_pi.ari.counter + 1;
                //        _session.m_pi.ari.login = 0;

                //        packet_func.session_send(packet_func.pacote248(_session.m_pi.ari), _session, 0);

                //        // D� 3 Grand Prix Ticket, por que � a primeira vez que o player loga no dia
                //        //sAttendanceRewardSystem.getInstance().sendGrandPrixTicket(_session);
                //        // D� 5 Key of fortune, por que � a primeira vez que o player loga no dia
                //        //sAttendanceRewardSystem.getInstance().sendFortuneKey(_session);
                //    }
                //}
            }
            catch (exception e)
            {
                Console.WriteLine(e.getFullMessageError());
            }
        }

        public void requestEnterOtherChannelAndLobby(Player _session, packet _packet)
        {
            try
            {

                // Lobby anterior que o player estava
                var lobby = _session.m_pi.lobby;

                var c = enterChannel(_session, _packet.ReadByte());

                if (c != null)
                    c.enterLobby(_session, lobby);

            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[GameServer.requestEnterOtherChannelAndLobby][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));

            }
        }

        public void requestExceptionClientMessage(Player _session, packet _packet)
        {
            byte tipo = _packet.ReadByte();

            var exception_msg = _packet.ReadPStr();
            if (tipo == 1)
            {
                //cheat?
            }
            _smp::message_pool.push(new message("[GameServer.requestExceptionClientMessage][Log] PLAYER[UID=" + (_session.m_pi.uid) + ", EXTIPO="
                    + ((ushort)tipo) + ", MSG=" + exception_msg + "]", type_msg.CL_ONLY_FILE_LOG));
            //
            onDisconnected(_session);//send desconection
        }

        public void requestLogin(Player _session, packet _packet)
        {
            new LoginSystem().requestLogin(_session, _packet);
        }

        public void requestNotifyNotDisplayPrivateMessageNow(Player _session, packet _packet)
        {
            try
            {
                string nickname = _packet.ReadPStr();

                if (nickname.empty())
                    throw new exception("[GameServer.requestNotifyNotDisplayPrivateMessageNow][Error] Player[UID=" + (_session.m_pi.uid)
                            + "] nao pode ver mensagem agora, mas o nickname de quem enviou a mensagem para ele eh invalido(empty). Hacker ou Bug.",
                            ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 0x750050, 0));
                // Procura o player pelo nickname, para ver se ele está online
                var s = (Player)FindSessionByNickname(nickname);
                if (s != null && s.isConnected())
                {
                    // Log
                    _smp::message_pool.push(new message("[GameServer.requestNotifyNotDisplayPrivateMessageNow][Log] Player[UID=" + (_session.m_pi.uid)
                            + "] recebeu mensagem do Player[UID=" + (s.m_pi.uid) + ", NICKNAME=" + nickname + "], mas ele nao pode ver a mensagem agora.", type_msg.CL_FILE_LOG_AND_CONSOLE));

                    packet_func.session_send(packet_func.pacote040(nickname, "", 4), s);

                }
            }
            catch (exception e)
            {
                _smp::message_pool.push(new message("[GameServer.requestNotifyNotDisplayPrivateMessageNow][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        public void requestPlayerInfo(Player _session, packet _packet)
        {
            try
            {
                uint uid = _packet.ReadUInt32();
                byte season = _packet.ReadByte();
                _smp.message_pool.push(new message($"[GameServer.requestPlayerInfo][Log] PLAYER[UID: {_session.m_pi.uid}, R_UID: {uid}, SEASON: {(int)season}]", type_msg.CL_ONLY_CONSOLE));

                Player s = null;
                PlayerInfo pi = null;
                CharacterInfo ci = new CharacterInfo();

                if (uid == _session.m_pi.uid)
                {

                    pi = _session.m_pi;

                }
                else if ((s = findPlayer(uid)) != null)
                {
                    pi = s.m_pi;
                }
                else
                {

                    var cmd_mi = new CmdMemberInfo(uid);

                    NormalManagerDB.add(0, cmd_mi, null, null);

                    if (cmd_mi.getException().getCodeError() != 0)
                        throw cmd_mi.getException();

                    MemberInfoEx mi = cmd_mi.getInfo();

                    // Verifica se não é o mesmo UID, pessoas diferentes
                    // Quem quer ver a info não é GM aí verifica se o player é GM
                    if (uid != _session.m_pi.uid && !mi.capability.game_master/* & 4/*(GM)*/)
                    {

                        packet_func.session_send(packet_func.pacote089(uid, season, 3), _session);

                    }
                    else
                    {

                        List<MapStatisticsEx> v_ms_n, v_msa_n, v_ms_na, v_msa_na, v_ms_g, v_msa_g;

                        var cmd_ci = new CmdCharacterInfo(uid, CmdCharacterInfo.TYPE.ONE, -1);

                        NormalManagerDB.add(0, cmd_ci, null, null);

                        if (cmd_ci.getException().getCodeError() != 0)
                            throw cmd_ci.getException();

                        ci = cmd_ci.getInfo();

                        var cmd_ue = new CmdUserEquip(uid);

                        NormalManagerDB.add(0, cmd_ue, null, null);

                        if (cmd_ue.getException().getCodeError() != 0)
                            throw cmd_ue.getException();

                        UserEquip ue = cmd_ue.getInfo();

                        var cmd_ui = new CmdUserInfo(uid);

                        NormalManagerDB.add(0, cmd_ui, null, null);

                        if (cmd_ui.getException().getCodeError() != 0)
                            throw cmd_ui.getException();

                        UserInfoEx ui = cmd_ui.getInfo();

                        var cmd_gi = new CmdGuildInfo(uid, 0);

                        NormalManagerDB.add(0, cmd_gi, null, null);

                        if (cmd_gi.getException().getCodeError() != 0)
                            throw cmd_gi.getException();

                        var gi = cmd_gi.getInfo();

                        var cmd_ms = new CmdMapStatistics(uid, (CmdMapStatistics.TYPE_SEASON)(season), CmdMapStatistics.TYPE.NORMAL, CmdMapStatistics.TYPE_MODO.M_NORMAL);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_ms_n = cmd_ms.getMapStatistics();

                        cmd_ms.setType(CmdMapStatistics.TYPE.ASSIST);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_msa_n = cmd_ms.getMapStatistics();

                        cmd_ms.setType(CmdMapStatistics.TYPE.NORMAL);
                        cmd_ms.setModo(CmdMapStatistics.TYPE_MODO.M_NATURAL);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_ms_na = cmd_ms.getMapStatistics();

                        cmd_ms.setType(CmdMapStatistics.TYPE.ASSIST);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_msa_na = cmd_ms.getMapStatistics();

                        cmd_ms.setType(CmdMapStatistics.TYPE.NORMAL);
                        cmd_ms.setModo(CmdMapStatistics.TYPE_MODO.M_GRAND_PRIX);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_ms_g = cmd_ms.getMapStatistics();

                        cmd_ms.setType(CmdMapStatistics.TYPE.ASSIST);

                        NormalManagerDB.add(0, cmd_ms, null, null);

                        if (cmd_ms.getException().getCodeError() != 0)
                            throw cmd_ms.getException();

                        v_msa_g = cmd_ms.getMapStatistics();

                        var cmd_tei = new CmdTrophySpecial(uid, (CmdTrophySpecial.TYPE_SEASON)(season), CmdTrophySpecial.TYPE.NORMAL);

                        NormalManagerDB.add(0, cmd_tei, null, null);

                        if (cmd_tei.getException().getCodeError() != 0)
                            throw cmd_tei.getException();

                        List<TrofelEspecialInfo> v_tei = cmd_tei.getInfo();

                        var cmd_ti = new CmdTrofelInfo(uid, (CmdTrofelInfo.TYPE_SEASON)(season));

                        NormalManagerDB.add(0, cmd_ti, null, null);

                        if (cmd_ti.getException().getCodeError() != 0)
                            throw cmd_ti.getException();

                        TrofelInfo ti = cmd_ti.getInfo();

                        cmd_tei.setType(CmdTrophySpecial.TYPE.GRAND_PRIX);

                        NormalManagerDB.add(0, cmd_tei, null, null);

                        if (cmd_tei.getException().getCodeError() != 0)
                            throw cmd_tei.getException();

                        List<TrofelEspecialInfo> v_tegi = cmd_tei.getInfo();

                        packet_func.session_send(packet_func.pacote157(mi, season), _session);

                        packet_func.session_send(packet_func.pacote15E(uid, ci), _session);

                        packet_func.session_send(packet_func.pacote156(uid, ue, season), _session);

                        packet_func.session_send(packet_func.pacote158(uid, ui, season), _session);

                        packet_func.session_send(packet_func.pacote15D(uid, gi), _session);

                        packet_func.session_send(packet_func.pacote15C(uid, v_ms_na, v_msa_na, Convert.ToByte((season != 0) ? 0x33 : 0x0A)), _session);

                        packet_func.session_send(packet_func.pacote15C(uid, v_ms_g, v_msa_g, Convert.ToByte((season != 0) ? 0x34 : 0x0B)), _session);

                        packet_func.session_send(packet_func.pacote15B(uid, season), _session);

                        packet_func.session_send(packet_func.pacote15A(uid, v_tei, season), _session);

                        packet_func.session_send(packet_func.pacote159(uid, ti, season), _session);

                        packet_func.session_send(packet_func.pacote15C(uid, v_ms_n.ToList(), v_msa_n.ToList(), season), _session);

                        packet_func.session_send(packet_func.pacote257(uid, v_tegi, season), _session);

                        packet_func.session_send(packet_func.pacote089(uid, season), _session);
                    }

                    return;
                }

                // Verifica se não é o mesmo UID, pessoas diferentes
                // Quem quer ver a info não é GM aí verifica se o player é GM
                if (uid != _session.m_pi.uid && !pi.m_cap.game_master/* & 4/*(GM)*/)
                {
                    packet_func.session_send(packet_func.pacote089(uid, season, 3), _session);

                }
                else
                {

                    var pCi = pi.findCharacterById(pi.ue.character_id);

                    if (pCi != null)
                        ci = pCi;

                    List<MapStatisticsEx> v_ms_n = new List<MapStatisticsEx>(), v_msa_n = new List<MapStatisticsEx>(), v_ms_na = new List<MapStatisticsEx>(), v_msa_na = new List<MapStatisticsEx>(), v_ms_g = new List<MapStatisticsEx>(), v_msa_g = new List<MapStatisticsEx>();

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_ms_normal[i].best_score != 127)
                            v_ms_n.Add(pi.a_ms_normal[i]);

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_msa_normal[i].best_score != 127)
                            v_msa_n.Add(pi.a_msa_normal[i]);

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_ms_natural[i].best_score != 127)
                            v_ms_na.Add(pi.a_ms_natural[i]);

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_msa_natural[i].best_score != 127)
                            v_msa_na.Add(pi.a_msa_natural[i]);

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_ms_grand_prix[i].best_score != 127)
                            v_ms_g.Add(pi.a_ms_grand_prix[i]);

                    for (byte i = 0; i < MS_NUM_MAPS; ++i)
                        if (pi.a_msa_grand_prix[i].best_score != 127)
                            v_msa_g.Add(pi.a_msa_grand_prix[i]);

                    packet_func.session_send(packet_func.pacote157(pi.mi, season), _session);

                    packet_func.session_send(packet_func.pacote15E(pi.uid, ci), _session);

                    packet_func.session_send(packet_func.pacote156(pi.uid, pi.ue, season), _session);

                    packet_func.session_send(packet_func.pacote158(pi.uid, pi.ui, season), _session);

                    packet_func.session_send(packet_func.pacote15D(pi.uid, pi.gi), _session);

                    packet_func.session_send(packet_func.pacote15C(pi.uid, v_ms_na, v_msa_na, (byte)((season != 0) ? 0x33 : 0x0A)), _session);

                    packet_func.session_send(packet_func.pacote15C(pi.uid, v_ms_g, v_msa_g, (byte)((season != 0) ? 0x34 : 0x0B)), _session);

                    packet_func.session_send(packet_func.pacote15B(uid, season), _session);

                    packet_func.session_send(packet_func.pacote15A(pi.uid, (season != 0) ? pi.v_tsi_current_season : pi.v_tsi_rest_season, season), _session);

                    packet_func.session_send(packet_func.pacote159(pi.uid, (season != 0) ? pi.ti_current_season : pi.ti_rest_season, season), _session);

                    packet_func.session_send(packet_func.pacote15C(pi.uid, v_ms_n, v_msa_n, season), _session);

                    packet_func.session_send(packet_func.pacote257(pi.uid, (season != 0) ? pi.v_tgp_current_season : pi.v_tgp_rest_season, season), _session);


                    packet_func.session_send(packet_func.pacote089(uid, season), _session);

                }
            }
            catch (Exception e)
            {
                message_pool.push(new message($"[GameServer::RequestPlayerInfo][ErrorSystem] {e.Message}", type_msg.CL_ONLY_CONSOLE));
                packet_func.session_send(packet_func.pacote089(0), _session);
            }
        }

        public void requestPrivateMessage(Player _session, packet _packet)
        {
            PangyaBinaryWriter p = new PangyaBinaryWriter();
            Player s = null;
            string nickname = "";

            try
            {

                // Verifica se session está autorizada para executar esse ação, 
                // se ele não fez o login com o Server ele não pode fazer nada até que ele faça o login
                //    CHECK_SESSION_IS_AUTHORIZED("PrivateMessage");

                nickname = _packet.ReadPStr();
                string msg = _packet.ReadPStr();

                if (nickname.empty())
                    throw new exception("[GameServer.requestPrivateMessage][Error] player[UID=" + (_session.m_pi.uid) + "] tentou enviar message privada[msg=" + msg + "] para o player[NICKNAME="
                            + nickname + "], mas o nick esta vazio. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 1, 5));

                if (msg.empty())
                    throw new exception("[GameServer.requestPrivateMessage][Error] player[UID=" + (_session.m_pi.uid) + "] tentou enviar message privada[msg=" + msg + "] para o player[NICKNAME="
                        + nickname + "], mas message esta vazia. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 4, 5));

                // Verifica se o player tem os itens necessários(PREMIUM USER OR GM) para usar essa função
                if (nickname.Contains("#SC") || nickname.Contains("#CS"))
                {

                    // Só sai do Private message se for comando do Smart Calculator, se não faz as outras verificações para enviar o PM
                    //if (m_si.rate.smart_calculator && checkSmartCalculatorCmd(_session, msg, (nickname.compare("#SC") == 0 ? eTYPE_CALCULATOR_CMD::SMART_CALCULATOR : eTYPE_CALCULATOR_CMD::CALCULATOR_STADIUM)))
                    //    return;
                }

                s = (Player)FindSessionByNickname(nickname);

                if (s == null || !s.getState() || !s.isConnected())
                    throw new exception("[GameServer.requestPrivateMessage][WARNING] player[UID=" + (_session.m_pi.uid) + "] tentou enviar message privada[msg=" + msg + "] para o player[NICKNAME="
                            + nickname + "], mas o player nao esta online nesse server.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 2, 5));

                // Whisper Block
                if (!s.m_pi.whisper.IsTrue())
                    throw new exception("[GameServer.requestPrivateMessage][WARNING] player[UID=" + (_session.m_pi.uid) + "] tentou enviar message privada[msg=" + msg + "] para o player[NICKNAME="
                            + nickname + "], mas o whisper do player esta bloqueado.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 5, 4));

                if ((s.m_pi.lobby == 255/*não está na lobby*/ && s.m_pi.mi.sala_numero == ushort.MaxValue/*e não está em nenhum sala*/) || s.m_pi.place == 2)
                    throw new exception("[GameServer.requestPrivateMessage][WARNING] player[UID=" + (_session.m_pi.uid) + "] tentou enviar message privada[msg=" + msg + "] para o player[NICKNAME="
                            + nickname + "], mas o player nao pode receber message agora, por que nao pode ver o chat. pode estar no Papel Shop e Etc.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 3, 4));

                // Arqui procura por palavras inapropriadas na message

                // Envia para todo os GM do serve   r essa message
                var gm = FindAllGM();

                if (!gm.Any())
                {

                    var msg_gm = "\\5" + (_session.m_pi.nickname) + ">" + (s.m_pi.nickname) + ": '" + msg + "'";

                    foreach (Player el in gm)
                    {
                        if ((el.m_gi.whisper.IsTrue() || el.m_gi.isOpenPlayerWhisper(_session.m_pi.uid) || el.m_gi.isOpenPlayerWhisper(s.m_pi.uid))
                            && /*Nao envia o log de PM novamente para o GM que enviou ou recebeu PM*/(el.m_pi.uid != _session.m_pi.uid && el.m_pi.uid != s.m_pi.uid))
                        {
                            // Responde no chat do player
                            p.init_plain(0x40);

                            p.WriteByte(0);

                            p.WritePStr("\\1[PM]"); // Nickname

                            p.WritePStr(msg_gm);    // Message
                            packet_func.session_send(p, el);
                        }
                    }

                }

                // Log
                _smp::message_pool.push(new message("[PrivateMessage][Log] player[UID=" + (_session.m_pi.uid) + "] enviou a Message[" + msg + "] para o player[UID=" + (s.m_pi.uid) + "]", type_msg.CL_FILE_LOG_AND_CONSOLE));

                // Resposta para o que enviou a private message
                p.init_plain(0x84);

                p.WriteByte(0); // FROM

                p.WritePStr(s.m_pi.nickname);   // Nickname TO
                p.WritePStr(msg);
                packet_func.session_send(p, _session);

                // Resposta para o player que vai receber a private message
                p.init_plain(0x84);

                p.WriteByte(1); // TO

                p.WritePStr(_session.m_pi.nickname);    // Nickname FROM
                p.WritePStr(msg);
                packet_func.session_send(p, s);

                // Envia a mensagem para o Chat History do discord se ele estiver ativo

                // Verifica se o m_chat_discod flag está ativo para enviar o chat para o discord
                //     if (m_si.rate.smart_calculator && m_chat_discord)
                //sendMessageToDiscordChatHistory(
                //	"[PM]",                                                                                                             // From
                //             (_session.m_pi.nickname) + ">" + (s.m_pi.nickname) + ": '" + msg + "'"						// Msg
                //);

            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[GameServer.requestPrivateMessage][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));

                p.init_plain(0x40);

                p.WriteByte((ExceptionError.STDA_SOURCE_ERROR_DECODE(e.getCodeError()) == (uint)STDA_ERROR_TYPE.GAME_SERVER) ? (byte)ExceptionError.STDA_SYSTEM_ERROR_DECODE(e.getCodeError()) : 5);
                if (s != null && s.isConnected())
                    p.WritePStr(s.m_pi.nickname);
                else
                    p.WritePStr(nickname);  // Player não está online usa o nickname que ele forneceu
                packet_func.session_send(p, _session);
            }
        }

        public void requestQueueTicker(Player _session, packet _packet)
        {
            ////REQUEST_BEGIN("QueueTicker");

            PangyaBinaryWriter p = new PangyaBinaryWriter();

            try
            {

                //if (_session.m_pi.block_flag.m_flag.ticker)
                //    throw new exception("[GameServer.requestQueueTicker][Error] player[UID=" + (_session.m_pi.m_uid)
                //            + "] tentou abrir a fila do Ticker, mas o ticker esta bloqueado. Hacker ou Bug", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 10, 1/*UNKNOWN ERROR*/));

                var count = m_ticker.getSize();

                var time_left_milisecond = count * 30000;

                // Send Count Ticker and time left for send ticker
                p.init_plain(0xCA);

                p.WriteUInt16((ushort)count);
                p.WriteUInt32(time_left_milisecond);
                packet_func.session_send(p, _session);

            }
            catch (exception e)
            {

                _smp::message_pool.push(new message("[GameServer.requestQueueTicker][ErrorSystem] " + e.getFullMessageError(), type_msg.CL_FILE_LOG_AND_CONSOLE));

                // estou usando pacote de troca nickname, por que n�o sei qual o pangya manda, quando da erro no mandar ticker, nunca peguei esse erro
                p.init_plain(0x50);

                p.WriteUInt32((ExceptionError.STDA_SOURCE_ERROR_DECODE(e.getCodeError()) == (uint)STDA_ERROR_TYPE.GAME_SERVER) ? ExceptionError.STDA_SYSTEM_ERROR_DECODE(e.getCodeError()) : 1/*UNKNOWN ERROR*/);

                packet_func.session_send(p, _session);
            }
        }

        public void requestSendNotice(string notice)
        {
        }

        public void requestSendTicker(Player _session, packet _packet)
        {
        }

        public void requestTranslateSubPacket(Player _session, packet _packet)
        {
        }

        public void requestUCCSystem(Player _session, packet _packet)
        {
            _session.HandleUCC(_packet);
        }

        public void requestUCCWebKey(Player _session, packet _packet)
        {
        }

        public void sendChannelListToSession(Player _session)
        {
            try
            {
                var p = packet_func.pacote04D(v_channel);
                packet_func.session_send(p, _session);
            }
            catch (exception e)
            {
                _smp.message_pool.push("[GameServer.sendChannelListToSession][ErrorSystem] " + e.getFullMessageError());
            }
        }

        public override bool CheckCommand(string commandLine)
        {
            throw new NotImplementedException();
        }


        public PangyaTimer makeTime(uint milliseconds, List<uint> intervalTable = null)
        {
            var _timer = new PangyaTimer(milliseconds);
             
            return _timer;
        }


        public PangyaTimer unMakeTime(PangyaTimer _timer)
        {
            if (_timer == null)
                throw new exception("[game_server::unMakeTime][Error] tentou deletar o timer, mas o argumento eh nullptr", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 52, 0));

            if (_timer.getState() == PangyaTimer.STATE_TIME.RUN)
                _timer.stop();
            //m_timer_manager.deleteTimer(_timer);
            return _timer;
        }

        public string getClientVersionSideServer()
        {
            return m_si.version_client;
        }
    }
}

namespace sgs
{
    public class gs : Singleton<Pangya_GameServer.GameServerTcp.GameServer>
    {
    }
}