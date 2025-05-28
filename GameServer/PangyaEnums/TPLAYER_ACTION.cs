﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pangya_GameServer.PangyaEnums
{
    public enum TPLAYER_ACTION : byte
    {
        PLAYER_ACTION_ROTATION = 0,
        PLAYER_ACTION_MOTION_ROOM = 1,
        PLAYER_ACTION_LOUNGER_LOC = 4,
        PLAYER_ACTION_LOUNGER_STATE = 5,
        PLAYER_ACTION_MOVE = 6,
        PLAYER_ACTION_MOTION_LOUNGER = 7,
        PLAYER_ACTION_ACK_PLAYER = 8,
        PLAYER_ACTION_UNK_NULL = 9,
        PLAYER_ANIMATION_WITH_EFFECTS = 10
    }
}
