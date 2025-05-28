﻿using PangyaAPI.Network.Pangya_St;
using PangyaAPI.Network.PangyaPacket;

namespace PangyaAPI.Network.PangyaUnit
{
    public interface IUnitAuthServer
    {
        void authCmdShutdown(int _time_sec);
        void authCmdBroadcastNotice(string _notice);
        void authCmdBroadcastTicker(string _nickname, string _msg);
        void authCmdBroadcastCubeWinRare(string _msg, uint _option);
        void authCmdDisconnectPlayer(uint _req_server_uid,
            uint _player_uid,
            byte _force);
        void authCmdConfirmDisconnectPlayer(uint _player_uid);
        void authCmdNewMailArrivedMailBox(uint _player_uid, uint _mail_id);
        void authCmdNewRate(uint _tipo, uint _qntd);
        void authCmdReloadGlobalSystem(uint _tipo);
        void authCmdInfoPlayerOnline(uint _req_server_uid, uint _player_uid);
        void authCmdConfirmSendInfoPlayerOnline(uint _req_server_uid, AuthServerPlayerInfo _aspi);

        // requests Comandos e respostas dinâmicas
        void authCmdSendCommandToOtherServer(packet _packet);
        void authCmdSendReplyToOtherServer(packet _packet);

        // Server envia comandos e resposta para outros server com o Auth Server
        void sendCommandToOtherServerWithAuthServer(packet _packet, uint _send_server_uid_or_type);
        void sendReplyToOtherServerWithAuthServer(packet _packet, uint _send_server_uid_or_type);
    }
}
