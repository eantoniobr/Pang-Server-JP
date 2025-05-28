﻿using System;
using PangyaAPI.Network.Pangya_St;
using PangyaAPI.SQL;

namespace PangyaAPI.Network.Cmd
{
    public class CmdChatMacroUser : Pangya_DB
    {
        uint m_uid = 0;
        chat_macro_user m_macro_user;
        public CmdChatMacroUser(uint _uid)
        {
            m_macro_user = new chat_macro_user();
            m_uid = _uid;
        }

        protected override void lineResult(ctx_res _result, uint _index_result)
        {
            checkColumnNumber(9);
            try
            {
                for (int i = (int)0u; i < 9u; i++)
                {

                    if (_result.data[i] != null)
                    {
                        var _chat = _result.data[i].ToString();
                        // var _chat = verifyAndTranslate(_result.data[i].ToString(), 2/*fixed size*/);
                        // !@ possivel erro de violação de acesso
                        if (!string.IsNullOrEmpty(_chat))
                        {
                            try
                            {
                                m_macro_user.macro[i] = _result.data[i].ToString();
                            }
                            catch (Exception)
                            {

                                throw;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }

        protected override Response prepareConsulta()
        {
            var r = procedure("pangya.ProcGetMacrosUser", m_uid.ToString());
            checkResponse(r, "nao conseguiu pegar o macro do player: " + (m_uid));
            return r;
        }


        public chat_macro_user getMacroUser()
        {
            return m_macro_user;
        }

    }
}
