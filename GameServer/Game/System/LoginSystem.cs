﻿using System;
using System.Collections.Generic;
using System.Linq;
using Pangya_GameServer.Cmd;
using Pangya_GameServer.Game.Manager;
using Pangya_GameServer.GameType;
using Pangya_GameServer.PacketFunc;
using Pangya_GameServer.Session;
using PangyaAPI.IFF.JP.Extensions;
using PangyaAPI.Network.Cmd;
using PangyaAPI.Network.Pangya_St;
using PangyaAPI.Network.PangyaPacket;
using PangyaAPI.Network.PangyaSession;
using PangyaAPI.SQL;
using PangyaAPI.SQL.Manager;
using PangyaAPI.Utilities;
using PangyaAPI.Utilities.BinaryModels;
using PangyaAPI.Utilities.Log;
using static Pangya_GameServer.GameType._Define;
using static Pangya_GameServer.GameType.PlayerInfo;
using _smp = PangyaAPI.Utilities.Log;

namespace Pangya_GameServer.Game.System
{
    /// <summary>
    /// Class manipulation login player!
    /// checks and funcs
    /// </summary>
    public class LoginSystem
    {
        uint m_count;
        /// <summary>
        /// requestCommonCmdGM login 
        /// </summary>
        /// <param db_name="_Packet">bits recevied by projectg</param>
        /// <param db_name="_session">client = session</param>
        public void requestLogin(Player _session, packet _packet)
        {
            PangyaBinaryWriter p;

            try
            {

                uint Packet_version = 0;

                KeysOfLogin kol = new KeysOfLogin();
                AuthKeyInfo akli = new AuthKeyInfo();
                AuthKeyGameInfo akgi = new AuthKeyGameInfo();

                string client_version;


                //////////// ----------------------- Começa a ler o Packet que o cliente enviou ------------------------- \\\\\\\\\\\
                // Read Packet Client request
                _session.m_pi.id = _packet.ReadString();
                _session.m_pi.uid = _packet.ReadUInt32();
                var ntKey = _packet.ReadUInt32(); // ntKey
                var Command = _packet.ReadUInt16();
                kol.keys[0] = _packet.ReadString();
                client_version = _packet.ReadString();
                Packet_version = _packet.ReadUInt32();
                string mac_address = _packet.ReadString();
                kol.keys[1] = _packet.ReadString();


                // -------------- Finished reading the Packet sent by the client ---------------


                ////////////----------------------- Terminou a leitura do Packet que o cliente enviou -------------------------\\\\\\\\\\\/

                // Verifica aqui se o IP/MAC ADDRESS do player está bloqueado
                if (sgs.gs.getInstance().haveBanList(_session.getIP(), mac_address, !mac_address.empty()))
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + ", IP="
                            + _session.getIP() + ", MAC=" + mac_address + "] esta bloqueado por regiao IP/MAC Addrress.");

                // Aqui verifica se recebeu os dados corretos
                if (_session.m_pi.id[0] == '\0')
                {
                    throw new exception("Player[UID=" + (_session.m_pi.uid)
                            + ", IP=" + _session.getIP() + "] id que o player enviou eh invalido. id: " + (_session.m_pi.id));
                }
                // Verifica se o gs está mantle, se tiver verifica se o player tem capacidade para entrar
                var cmd_pi = new CmdPlayerInfo(_session.m_pi.uid); // Waiter

                NormalManagerDB.add(0, cmd_pi, null, null);

                if (cmd_pi.getException().getCodeError() != 0)
                    throw cmd_pi.getException();

                //set info player!
                _session.m_pi.set_info(cmd_pi.getInfo());

                if (_session.m_pi.uid <= 0)
                    throw new exception("player[UID=" + (_session.m_pi.uid) + "] nao existe no banco de dados");

                // UID de outro player ou enviou o ID errado mesmo (essa parte é anti-hack ou bot)
                if (string.Compare(cmd_pi.getInfo().id, _session.m_pi.id) != 0)
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + ", REQ_UID="
                            + (_session.m_pi.uid) + "] Player ID nao bate : client send ID : " + (_session.m_pi.id) + "\t player DB ID : "
                            + (_session.m_pi.id));

                // Verifica aqui se a conta do player está bloqueada
                if (_session.m_pi.block_flag.m_id_state.ull_IDState != 0)
                {

                    if (_session.m_pi.block_flag.m_id_state.L_BLOCK_TEMPORARY && (_session.m_pi.block_flag.m_id_state.block_time == -1 || _session.m_pi.block_flag.m_id_state.block_time > 0))
                    {

                        throw new exception("[LoginSystem::requestLogin][Log] Bloqueado por tempo[Time="
                                + (_session.m_pi.block_flag.m_id_state.block_time == -1 ? ("indeterminado") : ((_session.m_pi.block_flag.m_id_state.block_time / 60)
                                + "min " + (_session.m_pi.block_flag.m_id_state.block_time % 60) + "sec"))
                                + "]. player [UID=" + (_session.m_pi.uid) + ", ID=" + (_session.m_pi.id) + "]");

                    }
                    else if (_session.m_pi.block_flag.m_id_state.L_BLOCK_FOREVER)
                    {

                        throw new exception("[LoginSystem::requestLogin][Log] Bloqueado permanente. player [UID=" + (_session.m_pi.uid)
                                + ", ID=" + (_session.m_pi.id) + "]");
                    }

                    else if (_session.m_pi.block_flag.m_id_state.L_BLOCK_ALL_IP)
                    {

                        // Bloquea todos os IP que o player logar e da error de que a area dele foi bloqueada

                        // Add o ip do player para a lista de ip banidos
                        NormalManagerDB.add(9, new CmdInsertBlockIp(_session.getIP(), "255.255.255.255"), sgs.gs.getInstance().SQLDBResponse, sgs.gs.getInstance());

                        // Resposta
                        throw new exception("[LoginSystem::requestLogin][Log] Player[UID=" + (_session.m_pi.uid) + ", IP=" + (_session.getIP())
                                + "] Block ALL IP que o player fizer login.");
                    }
                    else if (_session.m_pi.block_flag.m_id_state.L_BLOCK_MAC_ADDRESS)
                    {

                        // Bloquea o MAC Address que o player logar e da error de que a area dele foi bloqueada

                        // Add o MAC Address do player para a lista de MAC Address banidos
                        NormalManagerDB.add(10, new CmdInsertBlockMac(mac_address), sgs.gs.getInstance().SQLDBResponse, sgs.gs.getInstance());

                        // Resposta
                        throw new exception("[LoginSystem::requestLogin][Log] Player[UID=" + (_session.m_pi.uid)
                                + ", IP=" + (_session.getIP()) + ", MAC=" + mac_address + "] Block MAC Address que o player fizer login.");

                    }
                }

                
                // Verifica o Auth Key do player
                var cmd_akli = new CmdAuthKeyLoginInfo((int)_session.m_pi.uid); // Waiter

                NormalManagerDB.add(0, cmd_akli, null, null);

                if (cmd_akli.getException().getCodeError() != 0)
                    throw cmd_akli.getException();
                //false  = true, true = false
                // ### Isso aqui é uma falha de segurança faltal, muito grande nunca posso deixar isso ligado depois que colocar ele online
                if (!sgs.gs.getInstance().canSameIDLogin() && cmd_akli.getInfo().valid == 0)
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + "].\tAuthKey ja foi utilizada antes.", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 1056, 0));

                // ### Isso aqui é uma falha de segurança faltal, muito grande nunca posso deixar isso ligado depois que colocar ele online
                if (!sgs.gs.getInstance().canSameIDLogin() &&
            string.Compare(kol.keys[0], cmd_akli.getInfo().key) != 0
                )
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + "].\tAuthKey no bate(no match).", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.GAME_SERVER, 1057, 0));

                 
                // Member Info
                var cmd_mi = new CmdMemberInfo(_session.m_pi.uid);    // Waiter

                NormalManagerDB.add(0, cmd_mi, null, null);

                if (cmd_mi.getException().getCodeError() != 0)
                    throw cmd_mi.getException();

                _session.m_pi.mi = cmd_mi.getInfo();
                _session.m_pi.m_cap = cmd_mi.getCap();

                // Passa o Online ID para a estrutura MemberInfo, para não da erro depois 
                _session.m_pi.mi.oid = _session.m_oid;
                _session.m_pi.mi.state_flag.visible = true;
                _session.m_pi.mi.state_flag.whisper = _session.m_pi.whisper.IsTrue();
                _session.m_pi.mi.state_flag.channel = !_session.m_pi.whisper.IsTrue();//passar true?
                //tem que ser 6, pois o visible + whisper tem que compor 6
                if (_session.m_pi.m_cap.game_master)
                {
                    _session.m_gi.setGMUID(_session.m_pi.uid);    // Set o UID do GM dados

                    _session.m_pi.mi.state_flag.visible = _session.m_gi.visible;
                    _session.m_pi.mi.state_flag.whisper = _session.m_gi.whisper;
                    _session.m_pi.mi.state_flag.channel = _session.m_gi.channel;
                }

                // Verifica se o player tem a capacidade e level para entrar no gs
                if (sgs.gs.getInstance().m_si.propriedade.only_rookie && _session.m_pi.level >= 6/*Beginner E maior*/)
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + ", LEVEL="
                            + ((short)_session.m_pi.level) + "] nao pode entrar no gs por que o gs eh so para rookie.");
                /*Nega ele não pode ser nenhum para lançar o erro*/
                if (sgs.gs.getInstance().m_si.propriedade.mantle && !(_session.m_pi.m_cap.mantle || _session.m_pi.m_cap.game_master))
                    throw new exception("Player[UID=" + (_session.m_pi.uid) + ", CAP=" + (_session.m_pi.m_cap.ulCapability)
                            + "] nao tem a capacidade para entrar no gs mantle.");
                // Verifica se o Player já está logado
                var player_logado = sgs.gs.getInstance().HasLoggedWithOuterSocket(_session);

                if (player_logado != null)
                {
                    if (!sgs.gs.getInstance().canSameIDLogin())
                    {
                        _smp.message_pool.push("[LoginSystem::requestLogin][Log] Player[UID=" + (_session.m_pi.uid) + ", OID="
                            + (_session.m_oid) + ", IP=" + _session.getIP() + "] que esta logando agora, ja tem uma outra session com o mesmo UID logado, desloga o outro Player[UID="
                            + (player_logado.getUID()) + ", OID=" + (player_logado.m_oid) + ", IP=" + player_logado.getIP() + "]", type_msg.CL_FILE_LOG_AND_CONSOLE);

                        if (!sgs.gs.getInstance().DisconnectSession(player_logado))
                            throw new exception("Nao conseguiu disconnectar o player[UID=" + (player_logado.getUID())
                                + ", OID=" + (player_logado.m_oid) + ", IP=" + player_logado.getIP() + "], ele pode esta com o bug do oid bloqueado, ou SessionBase::UsaCtx bloqueado.");
                    }
                }

                // Junta Flag de block do gs, ao do player
                _session.m_pi.block_flag.m_flag.ullFlag |= sgs.gs.getInstance().m_si.flag.ullFlag;
                _session.m_pi.m_cap = _session.m_pi.mi.capability;//seta cap
                // Authorized a ficar online no gs por tempo indeterminado
                _session.m_is_authorized = true;

                // Registra no Banco de dados que o player está logado no Game Server
                NormalManagerDB.add(5, new CmdRegisterLogon(_session.m_pi.uid, 0/*Logou*/), sgs.gs.getInstance().SQLDBResponse, sgs.gs.getInstance());

                // Resgistra o Login do Player no gs
                NormalManagerDB.add(7, new CmdRegisterLogonServer(_session.m_pi.uid, sgs.gs.getInstance().m_si.uid), sgs.gs.getInstance().SQLDBResponse, sgs.gs.getInstance());

                _smp.message_pool.push("[LoginSystem::requestLogin][Log] Player[OID=" + (_session.m_oid) + ", UID=" + (_session.m_pi.uid) + ", NICK="
                        + (_session.m_pi.nickname) + ", Stage= Check].");

                //// Verifica se o papel tem limite por dia, se não anula o papel shop do player
                sPapelShopSystem.getInstance().init_player_papel_shop_info(_session);

                //NormalManagerDB.add(11, new CmdFirstAnniversary(), sgs.gs.getInstance().SQLDBResponse, this);  

                NormalManagerDB.add(2, new CmdUserEquip(_session.m_pi.uid), SQLDBResponse, _session);

                // Time que check o TTL, para prevenção de Bots mal feitos
                _session.m_tick_bot = Environment.TickCount;

                // Entra com sucesso 
                // Entra com sucesso
                packet_func.session_send(packet_func.pacote044(sgs.gs.getInstance().m_si, 0xD3, _session.m_pi), _session, 0);

            }
            catch (exception ex)
            {
                _smp.message_pool.push(new message(
             $"[LoginSystem::requestLogin][ErrorSt] {ex.Message}\nStack Trace: {ex.StackTrace}",
             type_msg.CL_FILE_LOG_AND_CONSOLE));

                // Error no login, set falso o varriza o player a continuar conectado com o Game Server
                _session.m_is_authorized = false;

                // Error Sistema
                p = new PangyaBinaryWriter(0x44);
                // Pronto agora sim, mostra o erro que eu quero
                p.WriteUInt32(300);

                packet_func.session_send(p, _session);

                // Disconnect

                sgs.gs.getInstance().DisconnectSession(_session);
            }
        }

        void SQLDBResponse(int _msg_id, Pangya_DB _pangya_db, object _arg)
        {
            if (_arg == null)
            {
                _smp.message_pool.push("[LoginSystem.SQLDBResponse][Error] _arg is null na msg_id = " + (_msg_id));
                return;
            }
            // if (_arg is LoginTask && (_session = (LoginTask)_arg) != null)

            var _session = (Player)_arg;

            try
            {
                // Verifica se a session ainda é valida, essas funções já é thread-safe
                if (_session == null || !_session.isConnected())
                    throw new exception("[SQLDBResponse][Error] session is invalid, para tratar o pangya_db");

                // Por Hora só sai, depois faço outro tipo de tratamento se precisar
                if (_pangya_db.getException().getCodeError() != 0)
                    throw new exception(_pangya_db.getException().getFullMessageError());

                switch (_msg_id)
                {
                    case 0: // Info Player
                        {

                            break;
                        }
                    case 1: // Key Login
                        {


                            break;
                        }
                    case 2: // Member Info - User Equip
                        {

                            var pi = _session.m_pi;

                            _session.m_pi.ue = ((CmdUserEquip)_pangya_db).getInfo();

                            // Verifica se tem o Pacote de verificação de bots ativado
                            int ttl = sgs.gs.getInstance().getBotTTL(); //10000÷1.000=10s

                            packet_func.session_send(packet_func.pacote1A9(ttl/*milliseconds*/), _session); // Tempo para enviar um pacote, ant Bot

                            NormalManagerDB.add(5, new CmdTutorialInfo(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(6, new CmdCouponGacha(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(7, new CmdUserInfo(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(8, new CmdGuildInfo(pi.uid, 0), SQLDBResponse, _session);

                            NormalManagerDB.add(9, new CmdDolfiniLockerInfo(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(10, new CmdCookie(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(11, new CmdTrofelInfo(pi.uid, CmdTrofelInfo.TYPE_SEASON.CURRENT), SQLDBResponse, _session);

                            // Esses que estavam aqui coloquei no resposta do CmdUserEquip, por que eles precisam da resposta do User Equip

                            NormalManagerDB.add(16, new CmdMyRoomConfig(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(18, new CmdCheckAchievement(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(20, new CmdDailyQuestInfoUser(pi.uid, CmdDailyQuestInfoUser.TYPE.GET), SQLDBResponse, _session);

                            NormalManagerDB.add(21, new CmdCardInfo(pi.uid, CmdCardInfo.TYPE.ALL), SQLDBResponse, _session);

                            NormalManagerDB.add(22, new CmdCardEquipInfo(pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(23, new CmdTrophySpecial(pi.uid, CmdTrophySpecial.TYPE_SEASON.CURRENT, CmdTrophySpecial.TYPE.NORMAL), SQLDBResponse, _session);

                            NormalManagerDB.add(24, new CmdTrophySpecial(pi.uid, CmdTrophySpecial.TYPE_SEASON.CURRENT, CmdTrophySpecial.TYPE.GRAND_PRIX), SQLDBResponse, _session);

                            break;
                        }
                    case 3: // User Equip - Desativa
                        {
                            break;
                        }
                    case 4: // Premium Ticket
                        {
                            var pi = _session.m_pi;

                            pi.pt = ((CmdPremiumTicketInfo)(_pangya_db)).getInfo();

                            ///Att Capability do player
                            ///Verifica se tem premium ticket para mandar o pacote do premium user e a comet
                            if (sPremiumSystem.getInstance().isPremiumTicket(pi.pt._typeid) && pi.pt.id != 0 && pi.pt.unix_sec_date > 0)
                            {

                                sPremiumSystem.getInstance().updatePremiumUser(_session);

                                _smp.message_pool.push("[SQLDBResponse][Log] Player[UID=" + (pi.uid) + "] is Premium User");
                            }

                            break;
                        }
                    case 5: // Tutorial Info
                        {

                            _session.m_pi.TutoInfo = ((CmdTutorialInfo)(_pangya_db)).getInfo();
                            // Manda pacote do tutorial aqui
                            packet_func.session_send(packet_func.pacote11F(_session.m_pi, 3/*tutorial info, 3 add do zero init*/), _session);

                            break;
                        }
                    case 6: // Coupon Gacha
                        {
                            _session.m_pi.cg = ((CmdCouponGacha)(_pangya_db)).getCouponGacha();

                            // Não sei se o que é esse pacote, então não sei o que ele busca no banco de dados, mas depois descubro
                            // Deixar ele enviando aqui por enquanto

                            packet_func.session_send(packet_func.pacote101(), _session);// pacote novo do JP

                            break;
                        }
                    case 7: // User Info
                        {

                            var pi = _session.m_pi;

                            pi.ui = ((CmdUserInfo)(_pangya_db)).getInfo();    // cmd_ui.getInfo();

                            NormalManagerDB.add(26, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.NORMAL, CmdMapStatistics.TYPE_MODO.M_NORMAL), SQLDBResponse, _session);

                            NormalManagerDB.add(27, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.ASSIST, CmdMapStatistics.TYPE_MODO.M_NORMAL), SQLDBResponse, _session);

                            NormalManagerDB.add(28, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.NORMAL, CmdMapStatistics.TYPE_MODO.M_NATURAL), SQLDBResponse, _session);

                            NormalManagerDB.add(29, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.ASSIST, CmdMapStatistics.TYPE_MODO.M_NATURAL), SQLDBResponse, _session);

                            NormalManagerDB.add(30, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.NORMAL, CmdMapStatistics.TYPE_MODO.M_GRAND_PRIX), SQLDBResponse, _session);

                            NormalManagerDB.add(31, new CmdMapStatistics(_session.m_pi.uid, CmdMapStatistics.TYPE_SEASON.CURRENT, CmdMapStatistics.TYPE.ASSIST, CmdMapStatistics.TYPE_MODO.M_GRAND_PRIX), SQLDBResponse, _session);

                            NormalManagerDB.add(36, new CmdChatMacroUser(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(38, new CmdFriendInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            break;
                        }
                    case 8: // Guild Info
                        {
                            _session.m_pi.gi = ((CmdGuildInfo)(_pangya_db)).getInfo();   // cmd_gi.getInfo(); 
                            break;
                        }
                    case 9:     // Donfini Locker Info
                        {
                            _session.m_pi.df = ((CmdDolfiniLockerInfo)(_pangya_db)).getInfo();   // cmd_df.getInfo();
                            break;
                        }
                    case 10:    // Cookie
                        {
                            _session.m_pi.cookie = ((CmdCookie)(_pangya_db)).getCookie();    // cmd_cookie.getCookie();

                            NormalManagerDB.add(32, new CmdMailBoxInfo2(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(33, new CmdCaddieInfo(_session.m_pi.uid, CmdCaddieInfo.TYPE.FERIAS), SQLDBResponse, _session);

                            NormalManagerDB.add(34, new CmdMsgOffInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(35, new CmdItemBuffInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(37, new CmdLastPlayerGameInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(39, new CmdAttendanceRewardInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(42, new CmdGrandPrixClear(_session.m_pi.uid), SQLDBResponse, _session);

                            NormalManagerDB.add(43, new CmdGrandZodiacPontos(_session.m_pi.uid, CmdGrandZodiacPontos.eCMD_GRAND_ZODIAC_TYPE.CGZT_GET), SQLDBResponse, _session);

                            NormalManagerDB.add(44, new CmdLegacyTikiShopInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            break;
                        }
                    case 11:    // Trofel Info atual
                        {
                            _session.m_pi.ti_current_season = ((CmdTrofelInfo)(_pangya_db)).getInfo();   // cmd_ti.getInfo();

                            NormalManagerDB.add(12, new CmdCharacterInfo(_session.m_pi.uid, CmdCharacterInfo.TYPE.ALL), SQLDBResponse, _session);

                            NormalManagerDB.add(13, new CmdCaddieInfo(_session.m_pi.uid, CmdCaddieInfo.TYPE.ALL), SQLDBResponse, _session);

                            NormalManagerDB.add(14, new CmdMascotInfo(_session.m_pi.uid, CmdMascotInfo.TYPE.ALL), SQLDBResponse, _session);

                            NormalManagerDB.add(15, new CmdWarehouseItem(_session.m_pi.uid, CmdWarehouseItem.TYPE.ALL), SQLDBResponse, _session);

                            break;
                        }
                    case 12:    // Character Info
                        {

                            var pi = _session.m_pi;

                            pi.mp_ce = ((CmdCharacterInfo)(_pangya_db)).getAllInfo(); // cmd_ci.getAllInfo();

                            pi.ei.char_info = null;

                            // Add Structure de estado do lounge para cada character do player
                            foreach (var el in pi.mp_ce)
                            {
                                if (!pi.mp_scl.ContainsKey(el.Value.id))
                                    pi.mp_scl.Add(el.Value.id, new StateCharacterLounge());
                            }

                            // Att Character Equipado que não tem nenhum character o player
                            if (pi.ue.character_id == 0 || pi.mp_ce.Count() <= 0)
                                pi.ue.character_id = 0;
                            else
                            { // Character Info(CharEquip)

                                // É um Map, então depois usa o find com a Key, que é mais rápido que rodar ele em um loop
                                var it = pi.mp_ce.Where(c => c.Key == pi.ue.character_id);

                                if (it.Any())
                                    pi.ei.char_info = it.First().Value;
                            }

                            // teste Calcula a condição do player e o sexo
                            // Só faz calculo de Quita rate depois que o player
                            // estiver no level Beginner E e jogado 50 games
                            if (pi.level >= 6 && pi.ui.jogado >= 50)
                            {
                                float rate = pi.ui.getQuitRate();

                                if (rate < GOOD_PLAYER_ICON)
                                    pi.mi.state_flag.azinha = true;
                                else if (rate >= QUITER_ICON_1 && rate < QUITER_ICON_2)
                                    pi.mi.state_flag.quiter_1 = true;
                                else if (rate >= QUITER_ICON_2)
                                    pi.mi.state_flag.quiter_2 = true;
                            }

                            if (pi.ei.char_info != null && pi.ui.getQuitRate() < GOOD_PLAYER_ICON)
                                pi.mi.state_flag.icon_angel = pi.ei.char_info.AngelEquiped() == 1 ? true : false;
                            else
                                pi.mi.state_flag.icon_angel = false;

                            pi.mi.state_flag.sexo = pi.mi.sexo == 1 ? true : false;

                            break;
                        }
                    case 13:    // Caddie Info
                        {

                            var pi = _session.m_pi;

                            pi.mp_ci = ((CmdCaddieInfo)(_pangya_db)).getInfo();   // cmd_cadi.getInfo();

                            // Check Caddie Times
                            player_manager.checkCaddie(_session);

                            pi.ei.cad_info = null;

                            // Att Caddie Equipado que não tem nenhum caddie o player
                            if (pi.ue.caddie_id == 0 || pi.mp_ci.Count() <= 0)
                                pi.ue.caddie_id = 0;
                            else
                            { // Caddie Info

                                // É um Map, então depois usa o find com a Key, qui é mais rápido que rodar ele em um loop
                                var it = pi.mp_ci.Where(c => c.Key == pi.ue.caddie_id);

                                if (it.Any())
                                    pi.ei.cad_info = it.First().Value;
                            }
                            break;
                        }
                    case 14:    // Mascot Info
                        {

                            var pi = _session.m_pi;

                            pi.mp_mi = ((CmdMascotInfo)(_pangya_db)).getInfo(); // cmd_mi.getInfo();

                            // Check Mascot Times
                            player_manager.checkMascot(_session);

                            // Att Mascot Equipado que não tem nenhum mascot o player
                            if (pi.ue.mascot_id == 0 || pi.mp_mi.Count() <= 0)
                                pi.ue.mascot_id = 0;
                            else
                            { // Mascot Info

                                // É um Map, então depois usa o find com a Key, qui é mais rápido que rodar ele em um loop
                                var it = pi.mp_mi.Where(c => c.Key == pi.ue.mascot_id);

                                if (it.Any())
                                    pi.ei.mascot_info = it.First().Value;
                            }
                            break;
                        }
                    case 15:    // Warehouse Item
                        {

                            var pi = _session.m_pi;

                            pi.mp_wi = ((CmdWarehouseItem)(_pangya_db)).getInfo();    // cmd_wi.getInfo();

                            // Check Warehouse Item Times
                            player_manager.checkWarehouse(_session);

                            // Iterator
                            Dictionary<stIdentifyKey, UpdateItem> ui_ticket_report_scroll;

                            //Verifica se tem Ticket Report Scroll no update item para abrir ele e excluir. Todos que estiver, não só 1
                            while ((ui_ticket_report_scroll = pi.findUpdateItemByTypeidAndType(TICKET_REPORT_SCROLL_TYPEID, UpdateItem.UI_TYPE.WAREHOUSE)).Count > 0)
                            {

                                try
                                {

                                    var pWi = pi.findWarehouseItemById(ui_ticket_report_scroll.FirstOrDefault().Value.id);

                                    if (pWi != null)
                                        item_manager.openTicketReportScroll(_session, pWi.id, ((pWi.c[1] * 0x800) | pWi.c[2]));

                                }
                                catch (exception e)
                                {

                                     if (e.getCodeError() == (int)STDA_ERROR_TYPE._ITEM_MANAGER)
                                        throw new exception("[SQLDBResponse][Error] " + e.getFullMessageError(), STDA_ERROR_TYPE.LOGIN_MANAGER);
                                    else
                                        throw;  // Relança
                                }
                            }


                            var it = pi.findWarehouseItemById(pi.ue.clubset_id);

                            // Att ClubSet Equipado que não tem nenhum clubset o player
                            if (pi.ue.clubset_id != 0 && it != null)
                            { // ClubSet Info

                                pi.ei.clubset = it;

                                // Esse C do WarehouseItem, que pega do DB, não é o ja updado inicial da taqueira é o que fica tabela enchant, 
                                // que no original fica no warehouse msm, eu só confundi quando fiz
                                // [AJEITEI JA] (tem que ajeitar na hora que coloca no DB e no DB isso)
                                pi.ei.csi.setValues(it.id, it._typeid, it.c);

                                var cs = sIff.getInstance().findClubSet(it._typeid);

                                if (cs != null)
                                {

                                    for (var i = 0u; i < 5; ++i)
                                        pi.ei.csi.enchant_c[i] = (short)(cs.SlotStats.getSlot[i] + it.clubset_workshop.c[i]);

                                }
                                else
                                    _smp.message_pool.push("[SQLDBResponse][Erro] player[UID=" + (pi.uid) + "] tentou inicializar ClubSet[TYPEID="
                                            + (it._typeid) + ", ID=" + (it.id) + "] equipado, mas ClubSet Not exists on IFF_STRUCT do Server. Bug");

                            }
                            else
                            {

                                it = pi.findWarehouseItemByTypeid(AIR_KNIGHT_SET);

                                if (it == null)
                                {

                                    pi.ue.clubset_id = it.id;
                                    pi.ei.clubset = it;

                                    //// Esse C do WarehouseItem, que pega do DB, não é o ja updado inicial da taqueira é o que fica tabela enchant, 
                                    //// que no original fica no warehouse msm, eu só confundi quando fiz
                                    //// [AJEITEI JA] (tem que ajeitar na hora que coloca no DB e no DB isso)
                                    pi.ei.csi.setValues(it.id, it._typeid, it.c);

                                    var cs = sIff.getInstance().findClubSet(it._typeid);

                                    if (cs != null)
                                    {
                                        for (var i = 0u; i < 5; ++i)
                                            pi.ei.csi.enchant_c[i] = (short)(cs.SlotStats.getSlot[i] + it.clubset_workshop.c[i]);

                                    }
                                    else
                                        _smp.message_pool.push("[SQLDBResponse][Erro] player[UID=" + (pi.uid) + "] tentou inicializar ClubSet[TYPEID="
                                                + (it._typeid) + ", ID=" + (it.id) + "] equipado, mas ClubSet Not exists on IFF_STRUCT do Server. Bug");


                                }
                                else
                                {   // Não tem add o ClubSet padrão para ele(CV1)

                                    _smp.message_pool.push("[SQLDBResponse][WARNING] Player[UID=" + (pi.uid)
                                            + "] nao tem o ClubSet[TYPEID=" + (AIR_KNIGHT_SET) + "] padrao.");

                                    BuyItem bi = new BuyItem();
                                    stItem item = new stItem();

                                    bi.id = -1;
                                    bi._typeid = AIR_KNIGHT_SET;
                                    bi.qntd = 1;

                                    item_manager.initItemFromBuyItem(pi, @item, bi, false, 0, 0, 1/*Não verifica o Level*/);

                                    if (item._typeid != 0 && (item.id = (int)item_manager.addItem(item, _session, 2/*Padrão item*/, 0)) != -4
                                        && (it = pi.findWarehouseItemById(item.id)) != null)
                                    {

                                        pi.ue.clubset_id = it.id;
                                        pi.ei.clubset = it;

                                        // Esse C do WarehouseItem, que pega do DB, não é o ja updado inicial da taqueira é o que fica tabela enchant, 
                                        // que no original fica no warehouse msm, eu só confundi quando fiz
                                        // [AJEITEI JA] (tem que ajeitar na hora que coloca no DB e no DB isso)
                                        pi.ei.csi.setValues(it.id, it._typeid, it.c);

                                        var cs = sIff.getInstance().findClubSet(it._typeid);

                                        if (cs != null)
                                        {

                                            for (var i = 0u; i < 5; ++i)
                                                pi.ei.csi.enchant_c[i] = (short)(cs.SlotStats.getSlot[i] + it.clubset_workshop.c[i]);

                                        }
                                        else
                                            _smp.message_pool.push("[SQLDBResponse][Erro] player[UID=" + (pi.uid) + "] tentou inicializar ClubSet[TYPEID="
                                                + (it._typeid) + ", ID=" + (it.id) + "] equipado, mas ClubSet Not exists on IFF_STRUCT do Server. Bug");


                                    }
                                    else
                                        throw new exception("[SQLDBResponse][Error] Player[UID=" + (pi.uid)
                                                + "] nao conseguiu adicionar o ClubSet[TYPEID=" + (AIR_KNIGHT_SET) + "] padrao para ele. Bug");

                                }
                            }

                            // Atualiza Comet(Ball) Equipada
                            var it_ball = pi.findWarehouseItemByTypeid(pi.ue.ball_typeid);
                            if (pi.ue.ball_typeid != 0)
                            {
                                pi.ei.comet = it_ball;
                            }
                            else
                            { // Default Ball

                                pi.ue.ball_typeid = DEFAULT_COMET_TYPEID;

                                it = pi.findWarehouseItemByTypeid(DEFAULT_COMET_TYPEID);

                                if (it != pi.mp_wi.LastOrDefault().Value)
                                {
                                    pi.ei.comet = it;
                                }
                                else
                                {   // não tem add a bola padrão para ele

                                    _smp.message_pool.push("[SQLDBResponse][WARNING] Player[UID=" + (_session.m_pi.uid)
                                            + "] nao tem a Comet(Ball)[TYPEID=" + (DEFAULT_COMET_TYPEID) + "] padrao.");

                                    BuyItem bi = new BuyItem();
                                    stItem item = new stItem();

                                    bi.id = -1;
                                    bi._typeid = DEFAULT_COMET_TYPEID;
                                    bi.qntd = 1;

                                    item_manager.initItemFromBuyItem(_session.m_pi, item, bi, false, 0, 0, 1/*Não verifica o Level*/);

                                    if (true)
                                    {

                                        _session.m_pi.ei.comet = it;

                                    }
                                    else
                                    {
                                        throw new exception("[SQLDBResponse][Error] Player[UID=" + (pi.uid)
                                                + "] nao conseguiu adicionar a Comet(Ball)[TYPEID=" + (DEFAULT_COMET_TYPEID) + "] padrao para ele. Bug");
                                    }

                                }
                            }

                            _session.m_pi.assist_flag = _session.m_pi.ItemExist(ASSIST_ITEM_TYPEID);

                            // Premium Ticket Tem que ser chamado depois que o Warehouse Item ja foi carregado
                            NormalManagerDB.add(4, new Cmd.CmdPremiumTicketInfo(_session.m_pi.uid), SQLDBResponse, _session);

                            break;
                        }
                    case 16:    // Config MyRoom
                        {

                            _session.m_pi.mrc = ((CmdMyRoomConfig)(_pangya_db)).getMyRoomConfig();   // cmd_mrc.getMyRoomConfig();

                            NormalManagerDB.add(17, new Cmd.CmdMyRoomItem(_session.m_pi.uid, CmdMyRoomItem.TYPE.ALL), SQLDBResponse, _session);
                            break;
                        }
                    case 17:    // MyRoom Item Info
                        {
                            _session.m_pi.v_mri = ((CmdMyRoomItem)(_pangya_db)).getMyRoomItem(); // cmd_mri.getMyRoomItem();

                            break;
                        }
                    case 18:    // Check if have Achievement
                        {
                            //// --------------------- AVISO ----------------------
                            //// esse aqui os outros tem que depender dele para, não ir sem ele
                            var cmd_cAchieve = (CmdCheckAchievement)(_pangya_db);

                            // Cria Achievements do player
                            if (!cmd_cAchieve.getLastState())
                            {
                                _session.m_pi.mgr_achievement.initAchievement(_session.m_pi.uid, true/*Create sem verifica se o player tem achievement, por que aqui ele já verificou*/);

                            //    Add o Task + 1 por que não pede o achievement do db, porque criou ele aqui e salvo no DB
                            //incremenetCount();

                            }
                            else
                            {
                                NormalManagerDB.add(19, new CmdAchievementInfo(_session.m_pi.uid), SQLDBResponse, _session);
                            }

                        }
                        break;
                    case 19:    // Achievement Info
                        {
                            var cmd_ai = ((CmdAchievementInfo)(_pangya_db));

                            // Inicializa o Achievement do player
                            _session.m_pi.mgr_achievement.initAchievement(_session.m_pi.uid, cmd_ai.GetInfo());

                            break;
                        }
                    case 20:    // Daily Quest User Info
                        {
                            _session.m_pi.dqiu = ((CmdDailyQuestInfoUser)(_pangya_db)).GetInfo();    // cmd_dqiu.getInfo();
                                                                                                     //                                                              // fim daily quest info player

                            break;
                        }
                    case 21:    // Card Info
                        {
                            _session.m_pi.v_card_info = ((CmdCardInfo)(_pangya_db)).getInfo();   // cmd_cardi.getInfo();

                            break;
                        }
                    case 22:    // Card Equipped Info
                        {
                            _session.m_pi.v_cei = ((CmdCardEquipInfo)(_pangya_db)).getInfo();    // cmd_cei.getInfo();

                            // Check Card Special Times
                            player_manager.checkCardSpecial(_session);

                            break;
                        }
                    case 23:    // Trofel especial normal atual
                        {
                            _session.m_pi.v_tsi_current_season = ((CmdTrophySpecial)(_pangya_db)).getInfo();

                            break;
                        }
                    case 24:    // Trofel especial grand prix atual
                        {
                            _session.m_pi.v_tgp_current_season = ((CmdTrophySpecial)_pangya_db).getInfo(); // cmd_tei.getInfo();

                            break;
                        }
                    case 26:    // MapStatistics normal, atual
                        {
                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();

                            try
                            {
                                foreach (var i in v_ms)
                                {
                                    _session.m_pi.a_ms_normal[i.course] = i;
                                }

                            }
                            catch (Exception ex)
                            {
                                throw ex;
                            }
                            break;
                        }
                    case 27:    // MapStatistics Normal, assist, atual
                        {
                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();

                            foreach (var i in v_ms)
                            {
                                _session.m_pi.a_msa_normal[i.course] = i;
                            }

                            break;
                        }
                    case 28:    // MapStatistics Natural, atual
                        {
                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();

                            foreach (var i in v_ms)
                            {
                                _session.m_pi.a_ms_natural[i.course] = i;
                            }

                            break;
                        }
                    case 29:    // MapStatistics Natural, assist, atual
                        {

                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();

                            foreach (var i in v_ms)
                            {
                                _session.m_pi.a_msa_natural[i.course] = i;
                            }

                            break;
                        }
                    case 30:    // MapStatistics GrandPrix, atual
                        {
                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();


                            foreach (var i in v_ms)
                            {
                                _session.m_pi.a_ms_grand_prix[i.course] = i;
                            }

                            break;
                        }
                    case 31:    // MapStatistics GrandPrix, Assist, atual
                        {
                            var v_ms = ((CmdMapStatistics)(_pangya_db)).getMapStatistics(); // cmd_ms.getMapStatistics();

                            foreach (var i in v_ms)
                            {
                                _session.m_pi.a_msa_grand_prix[i.course] = i;
                            }
                            break;
                        }
                    case 32:    // [MailBox] New Email(s), Agora é a inicialização do Cache do Mail Box
                        {
                            var cmd_mbi2 = ((CmdMailBoxInfo2)(_pangya_db));

                            _session.m_pi.m_mail_box.init(cmd_mbi2.getInfo(), _session.m_pi.uid);

                            var v_mb = _session.m_pi.m_mail_box.getAllUnreadEmail();

                            packet_func.session_send(packet_func.pacote210(v_mb), _session);
                            break;
                        }
                    case 33:    // Aviso Caddie Ferias
                        {
                            var v_cif = ((CmdCaddieInfo)(_pangya_db)).getInfo();    // cmd_cadi.getInfo();

                            if (v_cif.Any())
                            {

                                packet_func.session_send(packet_func.pacote0D4(v_cif), _session);
                            }
                            break;
                        }
                    case 34:    // Msg Off Info
                        {
                            var v_moi = ((CmdMsgOffInfo)(_pangya_db)).GetInfo();    // cmd_moi.getInfo();

                            if (!v_moi.Any())
                            {

                                packet_func.session_send(packet_func.pacote0B2(v_moi), _session);

                            }

                            break;
                        }
                    case 35:    // YamEquipedInfo ItemBuff(item que da um efeito, por tempo)
                        {
                            _session.m_pi.v_ib = ((CmdItemBuffInfo)(_pangya_db)).GetInfo();  // cmd_yei.getInfo();

                            //// Check Item Buff Times
                            player_manager.checkItemBuff(_session);

                            break;
                        }
                    case 36:    // Chat Macro User
                        {
                            _session.m_pi.cmu = ((CmdChatMacroUser)(_pangya_db)).getMacroUser();
                            break;
                        }
                    case 37:    // Last 5 Player Game Info
                        {
                            _session.m_pi.l5pg = ((CmdLastPlayerGameInfo)(_pangya_db)).getInfo();
                            break;
                        }
                    case 38:    // Friend List
                        {
                            _session.m_pi.mp_fi = ((CmdFriendInfo)(_pangya_db)).getInfo();
                            break;
                        }
                    case 39:    // Attendance Reward Info
                        {
                            _session.m_pi.ari = ((CmdAttendanceRewardInfo)(_pangya_db)).getInfo();
                            break;
                        }
                    case 40:    // Register Player Logon ON DB
                        {
                            // Não usa por que é um UPDATE
                            break;
                        }
                    case 41:    // Register Logon of player on Server in DB
                        {
                            // Não usa por que é um UPDATE
                            break;
                        }
                    case 42:    // Grand Prix Clear
                        {
                            _session.m_pi.v_gpc = ((CmdGrandPrixClear)(_pangya_db)).getInfo();

                            break;
                        }
                    case 43: // Grand Zodiac Pontos
                        {
                            _session.m_pi.grand_zodiac_pontos = ((CmdGrandZodiacPontos)(_pangya_db)).getPontos();

                            break;
                        }
                    case 44: // Legacy Tiki Shop(PointShop)
                        {
                            _session.m_pi.m_legacy_tiki_pts = ((CmdLegacyTikiShopInfo)(_pangya_db)).getInfo();

                            break;
                        }
                    default:
                        break;
                }

                // Incrementa o contador
                incremenetCount();

                if (getCount() == 39) // 44 - 5 (38 deixei o 1, 2, 3, 40 e 41 para o game server)
                    sendCompleteData(_session);
                else if (getCount() > 0)
                    sendReply(_session, _msg_id + 1);

                // Devolve (deixa a session livre) ou desconnecta ela se foi requisitado
                if (_session.devolve())
                {
                    _smp::message_pool.push(new message("[LoginManager::LoginManager][Test1] ", type_msg.CL_ONLY_CONSOLE));
                    sgs.gs.getInstance().DisconnectSession(_session);

                }

            }
            catch (exception ex)
            {
                _smp.message_pool.push(new message(
              $"[LoginSystem::SQLDBResponse][ErrorSystem] {ex.Message}\nStack Trace: {ex.StackTrace}",
              type_msg.CL_FILE_LOG_AND_CONSOLE));
                if (_session != null && _session.isConnected())
                    sgs.gs.getInstance().DisconnectSession(_session);
            }
        }

        protected void sendReply(Player _session, int _msg_id)
        {
            var p = new PangyaBinaryWriter(0x44);

            p.WriteByte(0xD2);
            p.WriteInt32(_msg_id);

            packet_func.session_send(p, _session, 1);
        }

        void sendCompleteData(Player _session)
        {
            //// Verifica se a session ainda é valida, essas funções já é thread-safe
            if (!_session.isConnected())
            {

                _smp.message_pool.push("[LoginSystem.sendCompleteData][Error] session is invalid.");
                _session.Disconnect();
                return;
            }

            try
            {
                //// Check All Character All Item Equiped is on Warehouse Item of Player
                foreach (var el in _session.m_pi.mp_ce)
                {
                    // Check Parts of Character e Check Aux Part of Character
                    _session.checkCharacterAllItemEquiped(el.Value);
                }

                // Check All Item Equiped
                _session.checkAllItemEquiped(_session.m_pi.ue);


                var pi = _session.m_pi;
                // Envia todos pacotes aqui, alguns envia antes, por que agora estou usando o jeito o pangya original   

                packet_func.session_send(packet_func.pacote044(sgs.gs.getInstance().m_si, 0, _session.m_pi), _session);

                packet_func.session_send(packet_func.pacote070(pi.mp_ce), _session); // characters

                packet_func.session_send(packet_func.pacote071(pi.mp_ci), _session); //caddies   

                packet_func.session_send(pi.mp_wi.Build(), _session); //inventory(warehouse)   

                packet_func.session_send(packet_func.pacote0E1(pi.mp_mi), _session); //mascots

                packet_func.session_send(packet_func.pacote072(pi.ue), _session); // equip selected                     

                sgs.gs.getInstance().sendChannelListToSession(_session);

                packet_func.session_send(packet_func.pacote102(pi), _session);        // Pacote novo do JP, passa os coupons do Gacha JP

                // Treasure Hunter Info
                packet_func.session_send(packet_func.pacote131(), _session);
                //call messenger server
                packet_func.session_send(packet_func.pacote0F1(), _session);

                _session.m_pi.mgr_achievement.sendCounterItemToPlayer(_session);

                _session.m_pi.mgr_achievement.sendAchievementToPlayer(_session);


                packet_func.session_send(packet_func.pacote144(), _session);        // Pacote novo do JP
                                                                                    //packet_func.session_send(packet_func.pacote0F1(), _session);

                // packet_func.session_send(packet_func.pacote135(), _session);

                packet_func.session_send(packet_func.pacote138(pi.v_card_info), _session);

                packet_func.session_send(packet_func.pacote136(), _session);

                packet_func.session_send(packet_func.pacote137(pi.v_cei), _session);
                //call messenger server
                packet_func.session_send(packet_func.pacote13F(), _session);

                packet_func.session_send(packet_func.pacote181(pi.v_ib), _session);

                packet_func.session_send(packet_func.pacote096(pi), _session);

                packet_func.session_send(packet_func.pacote169(pi.ti_current_season, 5/*season atual*/), _session);

                packet_func.session_send(packet_func.pacote169(pi.ti_rest_season), _session);

                packet_func.session_send(packet_func.pacote0B4(pi.v_tsi_current_season, 5/*season atual*/), _session);

                packet_func.session_send(packet_func.pacote0B4(pi.v_tsi_rest_season), _session);

                packet_func.session_send(packet_func.pacote158(pi.uid, pi.ui, 0), _session);
                //// Total de season, 5 atual season  
                packet_func.session_send(packet_func.pacote25D(pi.v_tgp_current_season, 5/*season atual*/), _session);

                packet_func.session_send(packet_func.pacote25D(pi.v_tgp_rest_season, 0), _session);
                //ver porque esta travando o login
                //UCCSystem.HandleUCCLoad(_session); 

                //// Login Reward System - verifica se o player ganhou algum item por logar
                //if (sgs.gs.getInstance().getInfo().rate.login_reward_event == 1)
                //    sLoginRewardSystem.getInstance().checkRewardLoginAndSend(_session);

                _smp.message_pool.push("[LoginSystem::requestLogin][Log] Player[OID=" + (_session.m_oid) + ", UID=" + (pi.uid) + ", NICK="
                       + (pi.nickname) + ", Stage= Sucess].");
            }
            catch (exception ex)
            {
                _smp.message_pool.push(new message($"[LoginSystem.requestLogin][ErrorSystem] {ex.getFullMessageError()}", type_msg.CL_FILE_LOG_AND_CONSOLE));
            }
        }

        uint getCount()
        {
            return m_count;
        }

        void incremenetCount()
        {
            ++m_count;
        }
    }
}
