﻿using System;
using PangyaAPI.SQL;

namespace Pangya_GameServer.Cmd
{
    public class CmdAddCounterItem : Pangya_DB
    {
        public CmdAddCounterItem(bool _waiter = false) : base(_waiter)
        {
            this.m_uid = 0;
            this.m_typeid = 0;
            this.m_value = 0;
            this.m_id = -1;
        }

        public CmdAddCounterItem(uint _uid,
            uint _typeid,
            uint _value,
            bool _waiter = false) : base(_waiter)
        {
            this.m_uid = _uid;
            this.m_typeid = _typeid;
            this.m_value = _value;
            this.m_id = -1;
        }
        public uint getUID()
        {
            return (m_uid);
        }

        public void setUID(uint _uid)
        {
            m_uid = _uid;
        }

        public uint getTypeid()
        {
            return (m_typeid);
        }

        public void setTypeid(uint _typeid)
        {
            m_typeid = _typeid;
        }

        public uint getValue()
        {
            return (m_value);
        }

        public void setValue(uint _value)
        {
            m_value = _value;
        }

        public int getId()
        {
            return (m_id);
        }

        protected override void lineResult(ctx_res _result, uint _index_result)
        {

            checkColumnNumber(1);

            m_id = IFNULL<int>(_result.data[0]);
        }

        protected override Response prepareConsulta()
        {

            m_id = -1;

            var r = procedure(m_szConsulta,
                Convert.ToString(m_uid) + ", " + Convert.ToString(m_typeid) + ", 1, " + Convert.ToString(m_value));

            checkResponse(r, "nao conseguiu adicionar o counter item[Typeid=" + Convert.ToString(m_typeid) + "] para o player: " + Convert.ToString(m_uid));

            return r;
        }

        private uint m_uid = new uint();
        private uint m_typeid = new uint();
        private uint m_value = new uint();
        private int m_id = new int();

        private const string m_szConsulta = "pangya.ProcAddCounterItem";
    }
}