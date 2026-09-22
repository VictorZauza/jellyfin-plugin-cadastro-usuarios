# Cadastro de Usuários — plugin para Jellyfin

Publica uma **tela pública de cadastro** (nome de usuário + senha) e um **painel no dashboard**
onde o administrador confirma cada solicitação apertando um botão.

- Compatível com **Jellyfin 10.11.x** (`net9.0`, `targetAbi 10.11.0.0`)
- Tela de cadastro responsiva: funciona bem no celular e no computador
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

## Botão "Criar minha conta" na tela de login

A tela de login do Jellyfin não tem botão de cadastro e nenhum plugin de servidor consegue
adicionar um por dentro dela — ela faz parte do `jellyfin-web`, um aplicativo já compilado.
O caminho suportado, que sobrevive a atualizações do servidor, usa dois campos do próprio
Jellyfin (Painel → **Geral** → seção **Marca**):

**1. Campo "Aviso legal no login":**

```
[Criar minha conta](/UserRegistration/Page)
```

> Use o caminho relativo (`/UserRegistration/Page`). Assim o botão continua funcionando se
> o domínio ou a porta mudarem.

**2. Campo "Código CSS personalizado":**

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

Salve e recarregue a tela de login: o botão aparece logo abaixo de **Entrar**, com a mesma
largura e altura dos outros botões, tanto no modo de lista de usuários quanto no login manual.

Variante contornada (deixa o "Entrar" como único botão azul) — troque o bloco do `a` por:

```css
#loginPage .loginDisclaimer a {
    display: block; box-sizing: border-box; width: 100%;
    margin: 0.25em 0; padding: 0.82em 1em;
    border: 2px solid #00a4dc; border-radius: 0.2em;
    background: transparent; color: #00a4dc;
    font-weight: 600; line-height: 1.35;
    text-align: center; text-decoration: none; transition: 0.2s;
}
#loginPage .loginDisclaimer a:hover,
#loginPage .loginDisclaimer a:focus {
    background: rgba(0, 164, 220, .15); color: #fff; text-decoration: none;
}
```

O botão abre a tela de cadastro em **nova aba** — isso vem do próprio Jellyfin, que força
`target="_blank"` em qualquer link desse campo, e não dá para mudar por CSS.

---

## Configurações do painel

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
