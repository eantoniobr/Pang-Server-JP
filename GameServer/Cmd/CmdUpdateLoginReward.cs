﻿using PangyaAPI.SQL;
using PangyaAPI.Utilities;
using System;

namespace Pangya_GameServer.Cmd
{
    public class CmdUpdateLoginReward : Pangya_DB
    {

        public CmdUpdateLoginReward(ulong _id,
            bool _is_end)
        {
            this.m_id = _id;
            this.m_is_end = _is_end;
        }

        public CmdUpdateLoginReward()
        {
            this.m_id = 0Ul;
            this.m_is_end = false;
        }

        public ulong getId()
        {
            return (m_id);
        }

        public void setId(ulong _id)
        {
            m_id = _id;
        }

        public bool getIsEnd()
        {
            return m_is_end;
        }

        public void setIsEnd(bool _is_end)
        {
            m_is_end = _is_end;
        }

        protected override void lineResult(ctx_res _result, uint _index_result)
        {

            // N�o usa por que � um UPDATE
            return;
        }

        protected override Response prepareConsulta()
        {

            if (m_id == 0Ul)
            {
                throw new exception("[CmdUpdateLoginReward::prepareConsulta][Error] m_id is invalid(" + Convert.ToString(m_id) + ")", ExceptionError.STDA_MAKE_ERROR_TYPE(STDA_ERROR_TYPE.PANGYA_DB,
                    4, 0));
            }

            var r = consulta(m_szConsulta[0] + (m_is_end ? "1" : "0") + m_szConsulta[1] + Convert.ToString(m_id));

            checkResponse(r, "nao conseguiu atualizar o Login Reward[ID=" + Convert.ToString(m_id) + ", IS_END=" + (m_is_end ? "TRUE" : "FALSE") + "]");

            return r;
        }

        private ulong m_id = new ulong();

        private bool m_is_end;

        private string[] m_szConsulta = { "UPDATE pangya.pangya_login_reward SET is_end = ", " WHERE index = " };
    }
}