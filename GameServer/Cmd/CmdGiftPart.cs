﻿//Convertion By LuisMK
using PangyaAPI.SQL;
using PangyaAPI.Utilities;
using System;

namespace Pangya_GameServer.Cmd
{
    public class CmdGiftPart : Pangya_DB
    {
        public CmdGiftPart(bool _waiter = false) : base(_waiter)
        {
            this.m_uid = 0u;
            this.m_id = -1;
        }

        public CmdGiftPart(uint _uid,
            int _id,
            bool _waiter = false) : base(_waiter)
        {
             this.m_uid = _uid;
             this.m_id = _id;
         }
    
        public uint getUID()
        {
            return (m_uid);
        }

        public void setUID(uint _uid)
        {
             m_uid = _uid;
         }

        public int getID()
        {
            return (m_id);
        }

        public void setID(int _id)
        {
             m_id = _id;
         }

        protected override void lineResult(ctx_res _result, uint _index_result)
        {

            // N�o usa por que � um UPDATE
            return;
        }

        protected override Response prepareConsulta()
        {

            if (m_id <= 0)
            {
                throw new exception("[CmdGiftPart::prepareConsulta][Error] Part id[value=" + Convert.ToString(m_id) + "] is invalid", STDA_MAKE_ERROR(STDA_ERROR_TYPE.PANGYA_DB,
                    4, 0));
            }

            var r = _update(m_szConsulta[0] + Convert.ToString(m_uid) + m_szConsulta[1] + Convert.ToString(m_id));

            checkResponse(r, "player[UID=" + Convert.ToString(m_uid) + "] nao conseguiu presentear Part[ID=" + Convert.ToString(m_id) + "]");

            return r;
        }
               
        private uint m_uid = new uint();
        private int m_id = new int();

        private string[] m_szConsulta = { "UPDATE pangya.pangya_item_warehouse SET Gift_flag = 1 WHERE UID = ", " AND item_id = " };
    }
}