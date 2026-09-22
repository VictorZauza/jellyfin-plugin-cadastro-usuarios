/*
 * Plugin "Cadastro de Usuários" para Jellyfin.
 * Coloca um botão "Criar conta" na tela de login e mostra o formulário ali mesmo,
 * sem trocar de página. Não altera nada em outras telas do cliente web.
 */
(function () {
    'use strict';

    var status = null;
    var loadingStatus = false;
    var styleInjected = false;

    function basePath() {
        var path = window.location.pathname || '';
        var idx = path.toLowerCase().indexOf('/web/');
        if (idx >= 0) {
            return path.substring(0, idx);
        }

        return '';
    }

    function api(route) {
        if (window.ApiClient && typeof window.ApiClient.getUrl === 'function') {
            try {
                return window.ApiClient.getUrl('UserRegistration/' + route);
            } catch (e) {
                /* cai no caminho relativo abaixo */
            }
        }

        return basePath() + '/UserRegistration/' + route;
    }

    function esc(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function injectStyle() {
        if (styleInjected) {
            return;
        }

        styleInjected = true;
        var css = [
            '.urPanel { margin: 0 auto; max-width: 30em; }',
            '.urPanel h1 { text-align: center; margin-bottom: .25em; }',
            '.urPanel .urLead { text-align: center; opacity: .75; margin: 0 0 1.4em; }',
            '.urPanel .urField { margin-bottom: 1.1em; }',
            '.urPanel label.urLabel { display: block; margin-bottom: .35em; font-weight: 600; font-size: .92em; }',
            '.urPanel input.urInput, .urPanel textarea.urInput {',
            '  width: 100%; box-sizing: border-box; font: inherit; font-size: 1em;',
            '  padding: .7em .75em; border-radius: .2em; border: 1px solid rgba(128,128,128,.45);',
            '  background: rgba(128,128,128,.12); color: inherit; }',
            '.urPanel input.urInput:focus, .urPanel textarea.urInput:focus {',
            '  outline: none; border-color: #00a4dc; }',
            '.urPanel textarea.urInput { resize: vertical; min-height: 4.5em; }',
            '.urPanel .urHint { opacity: .6; font-size: .8em; margin-top: .3em; }',
            '.urPanel .urMsg { display: none; border-radius: .2em; padding: .7em .8em; margin-bottom: 1em; font-size: .92em; }',
            '.urPanel .urMsg.urError { display: block; background: rgba(229,72,77,.15); color: #ff6369; }',
            '.urPanel .urMsg.urOk { display: block; background: rgba(48,164,108,.15); color: #4cc38a; }',
            '.urPanel .urDone { text-align: center; }',
            '.urPanel .urDone h2 { margin-bottom: .5em; }',
            '.urPanel .urDone p { opacity: .8; margin-bottom: 1.6em; }'
        ].join('\n');

        var tag = document.createElement('style');
        tag.id = 'urStyle';
        tag.textContent = css;
        document.head.appendChild(tag);
    }

    function loadStatus(done) {
        if (status) {
            done(status);
            return;
        }

        if (loadingStatus) {
            return;
        }

        loadingStatus = true;
        fetch(api('Public/Status'), { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                loadingStatus = false;
                if (!data) {
                    return;
                }

                status = {
                    enabled: data.Enabled !== undefined ? data.Enabled : data.enabled,
                    invite: data.RequiresInviteCode !== undefined ? data.RequiresInviteCode : data.requiresInviteCode,
                    minLength: data.MinimumPasswordLength || data.minimumPasswordLength || 6,
                    allowMessage: data.AllowMessage !== undefined ? data.AllowMessage : data.allowMessage,
                    title: data.Title || data.title || 'Criar conta',
                    welcome: data.WelcomeMessage || data.welcomeMessage || '',
                    success: data.SuccessMessage || data.successMessage || 'Solicitação enviada! Aguarde a aprovação do administrador.'
                };
                done(status);
            })
            .catch(function () { loadingStatus = false; });
    }

    function siblingsOf(panel) {
        var result = [];
        var parent = panel.parentNode;
        if (!parent) {
            return result;
        }

        for (var i = 0; i < parent.children.length; i++) {
            if (parent.children[i] !== panel) {
                result.push(parent.children[i]);
            }
        }

        return result;
    }

    function buildPanel(page, host) {
        injectStyle();

        var panel = document.createElement('div');
        panel.className = 'urPanel';
        panel.innerHTML = [
            '<h1>', esc(status.title), '</h1>',
            '<p class="urLead">', esc(status.welcome), '</p>',
            '<div class="urMsg" role="alert"></div>',
            '<form class="urForm" novalidate>',
            '  <div class="urField">',
            '    <label class="urLabel" for="urUser">Nome de usuário</label>',
            '    <input class="urInput" id="urUser" type="text" autocomplete="username" autocapitalize="off" spellcheck="false" maxlength="32">',
            '    <div class="urHint">De 3 a 32 caracteres. Letras, números e - _ . @ +</div>',
            '  </div>',
            '  <div class="urField">',
            '    <label class="urLabel" for="urPass">Senha</label>',
            '    <input class="urInput" id="urPass" type="password" autocomplete="new-password">',
            '    <div class="urHint">Pelo menos ', String(status.minLength), ' caracteres.</div>',
            '  </div>',
            '  <div class="urField">',
            '    <label class="urLabel" for="urPass2">Repita a senha</label>',
            '    <input class="urInput" id="urPass2" type="password" autocomplete="new-password">',
            '  </div>',
            status.invite ? [
                '  <div class="urField">',
                '    <label class="urLabel" for="urInvite">Código de convite</label>',
                '    <input class="urInput" id="urInvite" type="text" autocomplete="off" autocapitalize="off" spellcheck="false">',
                '  </div>'
            ].join('') : '',
            status.allowMessage ? [
                '  <div class="urField">',
                '    <label class="urLabel" for="urNote">Recado para o administrador (opcional)</label>',
                '    <textarea class="urInput" id="urNote" maxlength="280" rows="3"></textarea>',
                '  </div>'
            ].join('') : '',
            '  <button type="submit" class="raised button-submit block emby-button urSubmit"><span>Enviar solicitação</span></button>',
            '  <button type="button" class="raised cancel block emby-button urBack"><span>Voltar</span></button>',
            '</form>'
        ].join('');

        host.appendChild(panel);

        var hidden = siblingsOf(panel);
        hidden.forEach(function (el) {
            el.setAttribute('data-ur-prev', el.style.display || '');
            el.style.display = 'none';
        });

        function restore() {
            hidden.forEach(function (el) {
                el.style.display = el.getAttribute('data-ur-prev') || '';
                el.removeAttribute('data-ur-prev');
            });
            if (panel.parentNode) {
                panel.parentNode.removeChild(panel);
            }
        }

        panel.querySelector('.urBack').addEventListener('click', restore);

        var msg = panel.querySelector('.urMsg');
        function showMsg(text, kind) {
            msg.textContent = text;
            msg.className = 'urMsg ' + (kind === 'ok' ? 'urOk' : 'urError');
        }

        panel.querySelector('.urForm').addEventListener('submit', function (ev) {
            ev.preventDefault();
            msg.className = 'urMsg';

            var user = panel.querySelector('#urUser').value.trim();
            var pass = panel.querySelector('#urPass').value;
            var pass2 = panel.querySelector('#urPass2').value;
            var invite = panel.querySelector('#urInvite');
            var note = panel.querySelector('#urNote');

            if (!/^[a-zA-Z0-9\-_'.@+]{3,32}$/.test(user)) {
                showMsg('O nome de usuário deve ter de 3 a 32 caracteres e usar apenas letras, números e - _ . @ +');
                return;
            }

            if (pass.length < status.minLength) {
                showMsg('A senha precisa ter pelo menos ' + status.minLength + ' caracteres.');
                return;
            }

            if (pass !== pass2) {
                showMsg('As senhas não conferem.');
                return;
            }

            var submit = panel.querySelector('.urSubmit');
            submit.disabled = true;
            submit.querySelector('span').textContent = 'Enviando…';

            fetch(api('Public/Register'), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                body: JSON.stringify({
                    Username: user,
                    Password: pass,
                    ConfirmPassword: pass2,
                    InviteCode: invite ? invite.value : '',
                    Message: note ? note.value : ''
                })
            })
                .then(function (r) {
                    return r.text().then(function (body) {
                        var data = null;
                        try { data = body ? JSON.parse(body) : null; } catch (e) { data = null; }
                        if (!r.ok) {
                            throw new Error(
                                (data && (data.detail || data.Detail || data.title || data.Title))
                                || 'Não foi possível concluir o cadastro.');
                        }

                        return data;
                    });
                })
                .then(function (data) {
                    var text = (data && (data.Message || data.message)) || status.success;
                    panel.innerHTML = [
                        '<div class="urDone">',
                        '  <h2>Solicitação enviada</h2>',
                        '  <p>', esc(text), '</p>',
                        '  <button type="button" class="raised block emby-button urBack2"><span>Voltar para o login</span></button>',
                        '</div>'
                    ].join('');
                    panel.querySelector('.urBack2').addEventListener('click', restore);
                })
                .catch(function (err) {
                    showMsg(err.message || 'Não foi possível concluir o cadastro.');
                    submit.disabled = false;
                    submit.querySelector('span').textContent = 'Enviar solicitação';
                });
        });

        panel.querySelector('#urUser').focus();
    }

    function inject(page) {
        var host = page.querySelector('.readOnlyContent');
        if (!host || host.querySelector('.urCreateBtn')) {
            return;
        }

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'raised block emby-button urCreateBtn';
        btn.innerHTML = '<span>Criar conta</span>';
        btn.addEventListener('click', function () {
            buildPanel(page, host.parentNode || page);
        });

        host.insertBefore(btn, host.firstChild);
    }

    function scan() {
        try {
            var page = document.querySelector('#loginPage');
            if (!page) {
                return;
            }

            loadStatus(function (s) {
                if (s.enabled) {
                    inject(page);
                }
            });
        } catch (e) {
            /* nunca quebrar a tela de login por causa do plugin */
        }
    }

    function start() {
        scan();
        var observer = new MutationObserver(function () { scan(); });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
