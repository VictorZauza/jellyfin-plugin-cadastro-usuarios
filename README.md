# Cadastro de Usuários — plugin para Jellyfin

Publica uma **tela pública de cadastro** (nome de usuário + senha) e um **painel no dashboard**
onde o administrador confirma cada solicitação apertando um botão.

- Compatível com **Jellyfin 10.11.x** (`net9.0`, `targetAbi 10.11.0.0`)
- Tela de cadastro responsiva: funciona bem no celular e no computador
- Botão "Criar conta" na própria tela de login, com o formulário ali mesmo
- Testado de ponta a ponta contra um Jellyfin 10.11.5 real

---

## Como funciona

1. O visitante abre `http://SEU-SERVIDOR:8096/UserRegistration/Page` e escolhe usuário e senha.
2. O plugin cria a conta no Jellyfin **já desativada**. Ninguém entra com ela.
3. A solicitação aparece no painel do administrador (Dashboard → Plugins → Cadastro de Usuários).
4. O administrador aperta **Confirmar cadastro** → a conta é ativada e a pessoa já pode entrar
   com o mesmo usuário e senha que digitou.
5. Se apertar **Recusar**, a conta criada é apagada e o nome fica livre de novo.

**A senha nunca passa pelo plugin em texto guardado.** Ela vai direto para o cofre de senhas do
próprio Jellyfin no momento do cadastro; o administrador não vê a senha de ninguém.

---

## Instalação

### Opção 1 — pelo catálogo de plugins (recomendado)

1. No Jellyfin: **Painel → Plugins → Repositórios → +**
2. Nome: `Cadastro de Usuários`
   URL: `https://raw.githubusercontent.com/SEU-USUARIO/jellyfin-plugin-cadastro-usuarios/main/manifest.json`
3. Vá em **Catálogo**, categoria **General**, clique em **Cadastro de Usuários** e instale.
4. Reinicie o Jellyfin.

É o mesmo caminho dos plugins de terceiros do Jellyfin, e as próximas versões
aparecem sozinhas como atualização disponível.

### Opção 2 — manual

1. Copie a pasta `UserRegistration` (com `Jellyfin.Plugin.UserRegistration.dll` e `meta.json`)
   para a pasta de plugins do servidor:
   - Linux (pacote oficial): `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/` (dentro do contêiner)
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\` ou `%LocalAppData%\jellyfin\plugins\`
2. Reinicie o Jellyfin.
3. Painel → Plugins: o plugin **Cadastro de Usuários** aparece como *Active*.

> Em Docker, lembre-se de que a pasta de plugins precisa estar em um volume persistente,
> senão o plugin some quando o contêiner for recriado.

### Acesso pela internet

A tela fica no mesmo endereço público do servidor, por exemplo:
`https://jellyfin.seudominio.com.br/UserRegistration/Page`.
Se você usa proxy reverso, nenhuma regra nova é necessária — é a mesma porta da API.

---

## Publicando o repositório no GitHub

O repositório precisa de três coisas: o `manifest.json` na raiz, o zip do plugin em `dist/`
e o código-fonte. O script `publicar.sh` cuida do zip e do manifest (inclusive do checksum MD5,
que o Jellyfin confere na instalação):

```bash
./publicar.sh SEU-USUARIO-GITHUB
git add -A && git commit -m "v1.0.0.0" && git push
```

Para lançar uma versão nova:

```bash
./publicar.sh SEU-USUARIO-GITHUB jellyfin-plugin-cadastro-usuarios 1.1.0.0
git add -A && git commit -m "v1.1.0.0" && git push
```

O script já mantém a versão do assembly igual à do manifest — se as duas divergirem,
o Jellyfin oferece a atualização em loop.

**Detalhes que costumam quebrar um repositório de plugin:**

- A DLL tem que ficar na **raiz do zip**, sem pasta dentro. O Jellyfin extrai o zip direto
  na pasta do plugin; se houver uma pasta dentro do zip, o plugin não carrega.
- O `checksum` é o **MD5 do zip, em maiúsculas**. Se não bater, a instalação falha com
  "The checksum of the received data doesn't match".
- O `targetAbi` precisa ser menor ou igual à versão do servidor.
- O repositório precisa ser **público**, senão o Jellyfin não consegue baixar o manifest.

---

## Botão "Criar conta" na tela de login

A partir da versão 1.1.0.0 isso é **automático**: o plugin coloca um botão "Criar conta"
na tela de login e, ao clicar, o formulário aparece ali mesmo — sem trocar de página,
sem abrir aba. O visitante preenche, envia e a solicitação cai no painel do administrador.

Não é preciso configurar nada. Para desligar, desmarque **"Mostrar o formulário na tela
de login"** nas configurações do plugin.

### Como isso funciona

A tela de login faz parte do `jellyfin-web`, um aplicativo já compilado, e um plugin não
consegue entrar nela por dentro. O que o plugin faz é acrescentar uma linha no `index.html`
do cliente web, apontando para um script que ele mesmo serve:

```html
<script id="jellyfin-plugin-userregistration" defer
        src="configurationpage?name=UserRegistration.js&v=1.1.0.0"></script>
```

É a mesma técnica de plugins como Intro Skipper e Jellyscrub. Pontos que valem saber:

- **Atualização do Jellyfin apaga essa linha**, porque o `index.html` é substituído. O plugin
  refaz a marcação a cada inicialização do servidor, então volta sozinho.
- **Se a pasta do cliente web for somente leitura**, a marcação não é feita. O plugin registra
  um aviso no log e continua funcionando pelo endereço `/UserRegistration/Page`.
- O script só age na tela de login. Nas demais telas do Jellyfin ele não faz nada.

### Alternativa sem marcar o index.html

Se preferir não deixar o plugin alterar o cliente web, desmarque a opção e use os campos
nativos do Jellyfin (Painel → **Geral** → seção **Marca**). Aí o botão abre a tela de
cadastro em outra aba, em vez de mostrar o formulário ali mesmo:

**Campo "Aviso legal no login":**

```
[Criar minha conta](/UserRegistration/Page)
```

**Campo "Código CSS personalizado":**

```css
#loginPage .readOnlyContent { display: flex; flex-direction: column; }
#loginPage .loginDisclaimerContainer { display: block; order: -1; margin-top: 0; }
#loginPage .loginDisclaimer, #loginPage .loginDisclaimer p { margin: 0; }
#loginPage .loginDisclaimer a {
    display: block; box-sizing: border-box; width: 100%;
    margin: 0.25em 0; padding: 0.9em 1em;
    border-radius: 0.2em; background: #00a4dc; color: #fff;
    font-weight: 600; line-height: 1.35;
    text-align: center; text-decoration: none; transition: 0.2s;
}
#loginPage .loginDisclaimer a:hover,
#loginPage .loginDisclaimer a:focus {
    background: #0cb0e8; color: #fff; text-decoration: none;
}
```

Os arquivos `botao-login-azul.css` e `botao-login-contornado.css` do repositório trazem essa
variante e uma versão contornada.

---

## Configurações do painel

O plugin aparece na **barra lateral do painel**, na seção Plugins, como atalho direto
para esta tela.

| Configuração | Para que serve |
|---|---|
| Aceitar novos cadastros | Fecha ou abre a tela pública sem desinstalar o plugin |
| Código de convite | Se preenchido, só quem souber o código consegue solicitar |
| Tamanho mínimo da senha | Padrão: 6 caracteres |
| Máximo de solicitações pendentes | Impede que a fila seja inundada (0 = sem limite) |
| Solicitações por hora, por IP | Freia robôs (padrão 5, 0 = sem limite) |
| Permitir recado | Campo de texto opcional para o solicitante se identificar |
| Copiar permissões de | Usa um usuário existente como modelo ao aprovar |
| Permissões padrão | Bibliotecas, acesso remoto, download, transcodificação, sessões |
| Ocultar as contas aprovadas da tela de login | Tira os quadradinhos de usuário da tela de login (ligada por padrão) |
| Mostrar o formulário na tela de login | Liga/desliga o botão "Criar conta" dentro da tela de login |
| Textos da tela pública | Título, boas-vindas e mensagem de sucesso |

Poderes de administrador **nunca** são copiados do perfil modelo, e contas desativadas não
aparecem na lista de modelos.

---

## Endpoints

Públicos (sem login):

| Método | Rota | O que faz |
|---|---|---|
| GET | `/UserRegistration/Page` | A tela de cadastro (HTML) |
| GET | `/UserRegistration/Public/Status` | Se o cadastro está aberto, se exige convite, tamanho mínimo de senha |
| POST | `/UserRegistration/Public/Register` | Envia a solicitação |

Somente administrador (política `RequiresElevation` do Jellyfin):

| Método | Rota | O que faz |
|---|---|---|
| GET | `/UserRegistration/Requests` | Lista pendentes e histórico |
| POST | `/UserRegistration/Requests/{id}/Approve` | Confirma o cadastro |
| POST | `/UserRegistration/Requests/{id}/Reject` | Recusa e apaga a conta |
| DELETE | `/UserRegistration/Requests/{id}` | Remove do histórico |
| GET | `/UserRegistration/Users` | Usuários elegíveis como perfil modelo |

---

## Onde ficam os dados

- **Configurações**: `plugins/configurations/Jellyfin.Plugin.UserRegistration.xml`
- **Solicitações**: `plugins/Jellyfin.Plugin.UserRegistration/requests.json`

As solicitações ficam em arquivo próprio de propósito: assim, salvar as configurações no painel
não corre o risco de apagar uma solicitação que chegou enquanto a tela estava aberta.

---

## Compilando do código-fonte

Requer o .NET SDK 9.

```bash
cd Jellyfin.Plugin.UserRegistration
dotnet build -c Release
# a DLL sai em bin/Release/net9.0/Jellyfin.Plugin.UserRegistration.dll
```

Para outra versão do Jellyfin, ajuste a versão dos pacotes `Jellyfin.Controller` e
`Jellyfin.Model` no `.csproj` e o `targetAbi` no `build.yaml` e no `meta.json`.

---

## Licença

GPL-3.0, como exigido para plugins do Jellyfin (o plugin é linkado contra binários GPLv3).
