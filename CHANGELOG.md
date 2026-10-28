# Histórico de versões

## 2.2.0 — 2026-10-27

Primeira versão organizada para distribuição com instalador e pacote portátil.

### Recursos

- Automação ligada na abertura, com suporte a início em segundo plano junto com o Windows.
- Isolamento do monitor do Big Picture e restauração do layout anterior.
- Seleção do monitor principal ao voltar ao desktop.
- Cursor oculto após cinco segundos sem movimento durante o Big Picture.
- Seleção de saída de áudio de reprodução, com restauração e preservação da saída de comunicações.
- Ocultação temporária da barra de tarefas somente na tela do Big Picture em tela cheia.
- Painel escuro, cartões de monitores, ícones e switches.
- Recuperação após encerramento inesperado.

### Distribuição e desenvolvimento

- Separação entre código, recursos, testes, scripts e arquivos de execução.
- Dados do usuário em `%LOCALAPPDATA%\BigPictureControle`.
- Importação de preferências das versões antigas, sem copiar registros de recuperação.
- Testes de áudio e barra com interfaces simuladas, sem modificar dispositivos reais.
- Scripts de compilação, teste e empacotamento e workflow para Windows no GitHub Actions.

### Verificação

Os testes automatizados cobrem o controle de sessão e falhas de recuperação de áudio e barra. A validação de vídeo, jogos, foco e dispositivos reais depende do setup usado.
