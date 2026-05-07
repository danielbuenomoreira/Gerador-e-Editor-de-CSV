using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using CSV_GeradorEEditor.Models;
using CSV_GeradorEEditor.Services;

namespace CSV_GeradorEEditor
{
    public partial class MainForm : Form
    {
        private GeradorDadosService _geradorService;
        private ProcessadorArquivosService _processadorService;
        private CsvService _csvService;

        private List<RegistroPassante> _passantes;
        private List<RegistroPassante> _inadimplentes;
        private List<RegistroPassante> _daerNaoFiltrado;
        private List<RegistroPassante> _rejeitados;
        private List<RegistroPassante> _daer;
        private List<RegistroPagamento> _pagamentos;

        private object _listaAtualExibicao;
        private PropertyInfo[] _propriedadesAtuais;

        // Estrutura de altíssima performance para o Modo Livre
        private List<string[]> _dadosLivres;
        private List<string> _colunasLivres;

        private string _nomeArquivoSugerido = "PASSANTES.csv";
        private bool _modoEdicaoLivre = false;

        private DataGridView _dataGridView;
        private CheckBox _chkComCabecalho;
        private CheckBox _chkColunasVazias;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private FlowLayoutPanel _panelBotoesFluxo;

        private ComboBox _cmbColunaData;
        private DateTimePicker _dtpInicio;
        private DateTimePicker _dtpFim;

        public MainForm()
        {
            InitializeComponent();
            _geradorService = new GeradorDadosService();
            _processadorService = new ProcessadorArquivosService();
            _csvService = new CsvService();
            ConfigurarInterface();
        }

        // --- GERAÇÃO DE DADOS ---

        private async void BtnGerarPassantes_Click(object sender, EventArgs e)
        {
            int qtd = SolicitarQuantidade("Gerar PASSANTES (Max: 10.000.000)", 10000000, 0);
            if (qtd <= 0) return;

            TravarInterface(true); _lblStatus.Text = "Gerando Passantes...";
            var progresso = new Progress<int>(p => _progressBar.Value = p);
            _passantes = await Task.Run(() => _geradorService.GerarPassantes(qtd, progresso));
            ConfigurarExibicao("PASSANTES.csv", _passantes);
            TravarInterface(false);
        }

        private async void BtnGerarInadimplentes_Click(object sender, EventArgs e)
        {
            if (_passantes == null) { MessageBox.Show("Gere ou Importe os PASSANTES primeiro."); return; }
            int qtd = SolicitarQuantidade($"INADIMPLENTES a gerar:", _passantes.Count, _passantes.Count);
            if (qtd <= 0) return;

            TravarInterface(true); _lblStatus.Text = "Processando Inadimplentes...";
            _inadimplentes = await Task.Run(() => _geradorService.GerarSubconjunto(_passantes, qtd));
            ConfigurarExibicao("INADIMPLENTES.csv", _inadimplentes);
            TravarInterface(false);
        }

        private void BtnGerarDaerNaoFiltrado_Click(object sender, EventArgs e)
        {
            if (_inadimplentes == null) { MessageBox.Show("Gere ou Importe os INADIMPLENTES primeiro."); return; }
            _daerNaoFiltrado = new List<RegistroPassante>(_inadimplentes);
            ConfigurarExibicao("DAERnaoFiltrado.csv", _daerNaoFiltrado);
        }

        private async void BtnGerarRejeitados_Click(object sender, EventArgs e)
        {
            if (_daerNaoFiltrado == null) { MessageBox.Show("Gere ou Importe o DAER Não Filtrado primeiro."); return; }
            int qtd = SolicitarQuantidade($"REJEITADOS DAER a gerar:", _daerNaoFiltrado.Count, _daerNaoFiltrado.Count);
            if (qtd <= 0) return;

            TravarInterface(true);
            _rejeitados = await Task.Run(() => _geradorService.GerarSubconjunto(_daerNaoFiltrado, qtd));
            ConfigurarExibicao("REJEITADOSDAER.csv", _rejeitados);
            TravarInterface(false);
        }

        private async void BtnGerarDaerFinal_Click(object sender, EventArgs e)
        {
            if (_daerNaoFiltrado == null || _rejeitados == null) { MessageBox.Show("Gere o DAER Não Filtrado e os Rejeitados primeiro."); return; }
            TravarInterface(true);
            _daer = await Task.Run(() => _processadorService.ProcessarDaer(_daerNaoFiltrado, _rejeitados));
            ConfigurarExibicao("DAER.csv", _daer);
            TravarInterface(false);
        }

        private async void BtnGerarPagamentos_Click(object sender, EventArgs e)
        {
            if (_daer == null) { MessageBox.Show("Gere ou Importe o DAER final primeiro."); return; }
            int qtd = SolicitarQuantidade($"PAGAMENTOS a gerar:", _daer.Count, _daer.Count);
            if (qtd <= 0) return;

            TravarInterface(true);
            var progresso = new Progress<int>(p => _progressBar.Value = p);
            _pagamentos = await Task.Run(() => _geradorService.GerarPagamentos(_daer, qtd, _chkColunasVazias.Checked, progresso));
            ConfigurarExibicao("PAGAMENTOS.csv", _pagamentos);
            TravarInterface(false);
        }

        private async void BtnPreencherPagamentos_Click(object sender, EventArgs e)
        {
            if (_pagamentos == null || _modoEdicaoLivre) { MessageBox.Show("Você precisa estar na etapa original de PAGAMENTOS."); return; }
            TravarInterface(true);
            var progresso = new Progress<int>(p => _progressBar.Value = p);
            await Task.Run(() => _geradorService.PreencherPagamentosVazios(_pagamentos, progresso));
            _dataGridView.Invalidate();
            TravarInterface(false);
        }

        // --- IMPORTAÇÃO E EXPORTAÇÃO (WYSIWYG) ---

        private async void BtnImportar_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "CSV|*.csv" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Form dialogo = new Form() { Width = 400, Height = 250, Text = "Selecione o Fluxo", StartPosition = FormStartPosition.CenterScreen };
                    ComboBox cmbFluxo = new ComboBox() { Left = 20, Top = 20, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
                    cmbFluxo.Items.AddRange(new string[] { "1 - PASSANTES", "2 - INADIMPLENTES", "3 - DAER Não Filtrado", "4 - REJEITADOS DAER", "5 - DAER", "6 - PAGAMENTOS", "7 - MODO LIVRE (Para edições e imports genéricos)" });
                    cmbFluxo.SelectedIndex = 6; // Sugere modo livre
                    Button btnConfirmar = new Button() { Text = "Importar", Left = 130, Top = 100, Width = 120, DialogResult = DialogResult.OK };
                    dialogo.Controls.AddRange(new Control[] { cmbFluxo, btnConfirmar });

                    if (dialogo.ShowDialog() == DialogResult.OK)
                    {
                        TravarInterface(true); _lblStatus.Text = "Lendo arquivo CSV...";

                        try
                        {
                            if (cmbFluxo.SelectedIndex == 6)
                            {
                                await Task.Run(() => _csvService.ImportarModoLivre(ofd.FileName, out _colunasLivres, out _dadosLivres));
                                ConfigurarExibicaoLivre();
                            }
                            else
                            {
                                HabilitarModoLivre(false);
                                if (cmbFluxo.SelectedIndex == 5)
                                {
                                    _pagamentos = await Task.Run(() => _csvService.ImportarTipadoPagamento(ofd.FileName));
                                    ConfigurarExibicao("PAGAMENTOS.csv", _pagamentos);
                                }
                                else
                                {
                                    var dados = await Task.Run(() => _csvService.ImportarTipadoPassante(ofd.FileName));
                                    switch (cmbFluxo.SelectedIndex)
                                    {
                                        case 0: _passantes = dados; ConfigurarExibicao("PASSANTES.csv", _passantes); break;
                                        case 1: _inadimplentes = dados; ConfigurarExibicao("INADIMPLENTES.csv", _inadimplentes); break;
                                        case 2: _daerNaoFiltrado = dados; ConfigurarExibicao("DAERnaoFiltrado.csv", _daerNaoFiltrado); break;
                                        case 3: _rejeitados = dados; ConfigurarExibicao("REJEITADOSDAER.csv", _rejeitados); break;
                                        case 4: _daer = dados; ConfigurarExibicao("DAER.csv", _daer); break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"O arquivo não tem as colunas padrão.\n\nDetalhes: {ex.Message}", "Erro");
                        }
                        TravarInterface(false);
                    }
                }
            }
        }

        private async void BtnExportar_Click(object sender, EventArgs e)
        {
            if (_listaAtualExibicao == null && _dadosLivres == null) { MessageBox.Show("Não há dados para exportar."); return; }

            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "CSV|*.csv", FileName = _nomeArquivoSugerido })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    bool usarCabecalho = _chkComCabecalho.Checked;
                    string caminho = sfd.FileName;

                    TravarInterface(true); _lblStatus.Text = "Exportando exatamente como visualizado...";

                    // Captura a ordem visual que o usuário definiu na tela
                    var colunasGrid = _dataGridView.Columns.Cast<DataGridViewColumn>()
                                        .OrderBy(c => c.DisplayIndex).ToList();

                    List<string> cabecalhos = colunasGrid.Select(c => c.HeaderText).ToList();

                    await Task.Run(() =>
                    {
                        IEnumerable<string[]> ObterLinhas()
                        {
                            if (_listaAtualExibicao is System.Collections.IList listaModelos)
                            {
                                var propMap = colunasGrid.Select(c => _propriedadesAtuais.FirstOrDefault(p => p.Name == c.Name)).ToList();
                                foreach (var item in listaModelos)
                                {
                                    string[] linha = new string[colunasGrid.Count];
                                    for (int i = 0; i < colunasGrid.Count; i++)
                                    {
                                        var prop = propMap[i];
                                        if (prop != null)
                                        {
                                            var val = prop.GetValue(item);
                                            if (val is double d) linha[i] = d.ToString(CultureInfo.InvariantCulture);
                                            else linha[i] = val?.ToString() ?? "";
                                        }
                                    }
                                    yield return linha;
                                }
                            }
                            else if (_dadosLivres != null)
                            {
                                var indexMap = colunasGrid.Select(c => _colunasLivres.IndexOf(c.Name)).ToList();
                                foreach (var item in _dadosLivres)
                                {
                                    string[] linha = new string[colunasGrid.Count];
                                    for (int i = 0; i < colunasGrid.Count; i++)
                                    {
                                        int oIdx = indexMap[i];
                                        linha[i] = (oIdx >= 0 && oIdx < item.Length) ? item[oIdx] : "";
                                    }
                                    yield return linha;
                                }
                            }
                        }

                        _csvService.ExportarMatriz(cabecalhos, ObterLinhas(), caminho, usarCabecalho);
                    });

                    TravarInterface(false);
                    MessageBox.Show("Arquivo editado exportado com sucesso!");
                }
            }
        }

        // --- FILTRAGEM POR PERÍODO ---

        private async void BtnFiltrar_Click(object sender, EventArgs e)
        {
            if (_listaAtualExibicao == null && _dadosLivres == null) return;
            if (_cmbColunaData.SelectedItem == null) { MessageBox.Show("Selecione uma coluna de data."); return; }

            string nomeColunaGrid = _cmbColunaData.SelectedItem.ToString();
            string nomeColunaOriginal = _dataGridView.Columns.Cast<DataGridViewColumn>().First(c => c.HeaderText == nomeColunaGrid).Name;

            DateTime inicio = _dtpInicio.Value.Date;
            DateTime fim = _dtpFim.Value.Date;

            TravarInterface(true); _lblStatus.Text = "Recortando por período...";

            await Task.Run(() =>
            {
                if (_modoEdicaoLivre)
                {
                    int oIdx = _colunasLivres.IndexOf(nomeColunaOriginal);
                    _dadosLivres = _dadosLivres.AsParallel().Where(linha =>
                    {
                        if (oIdx >= 0 && oIdx < linha.Length)
                        {
                            if (DateTime.TryParseExact(linha[oIdx], "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime dt))
                                return dt >= inicio && dt <= fim;
                        }
                        return false;
                    }).ToList();
                }
                else
                {
                    if (_listaAtualExibicao is List<RegistroPagamento> lpag)
                    {
                        _pagamentos = _processadorService.FiltrarPorData(lpag, nomeColunaOriginal, inicio, fim);
                        _listaAtualExibicao = _pagamentos;
                    }
                    else if (_listaAtualExibicao is List<RegistroPassante> lpass)
                    {
                        var filtrado = _processadorService.FiltrarPorData(lpass, nomeColunaOriginal, inicio, fim);
                        if (_nomeArquivoSugerido == "PASSANTES.csv") _passantes = filtrado;
                        else if (_nomeArquivoSugerido == "INADIMPLENTES.csv") _inadimplentes = filtrado;
                        else if (_nomeArquivoSugerido == "DAERnaoFiltrado.csv") _daerNaoFiltrado = filtrado;
                        else if (_nomeArquivoSugerido == "REJEITADOSDAER.csv") _rejeitados = filtrado;
                        else if (_nomeArquivoSugerido == "DAER.csv") _daer = filtrado;
                        _listaAtualExibicao = filtrado;
                    }
                }
            });

            _dataGridView.RowCount = _modoEdicaoLivre ? _dadosLivres.Count : ((System.Collections.IList)_listaAtualExibicao).Count;
            _dataGridView.Invalidate();
            TravarInterface(false);
            MessageBox.Show("Filtro aplicado.");
        }

        // --- GRID: VIRTUAL MODE TOTAL E EDIÇÕES ---

        private void ConfigurarExibicaoLivre()
        {
            _listaAtualExibicao = null;
            _dataGridView.Columns.Clear();
            foreach (var col in _colunasLivres) _dataGridView.Columns.Add(col, col);
            _dataGridView.RowCount = _dadosLivres.Count;
            _dataGridView.Invalidate();
            HabilitarModoLivre(true);
            AtualizarColunasDeFiltro();
        }

        private void ConfigurarExibicao<T>(string nomeSugestao, List<T> dados)
        {
            _nomeArquivoSugerido = nomeSugestao;
            _listaAtualExibicao = dados;

            // Solução para obrigar a herança a ficar em ordem:
            if (typeof(T) == typeof(RegistroPagamento))
            {
                var baseProps = typeof(RegistroPassante).GetProperties();
                var derivedProps = typeof(RegistroPagamento).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                _propriedadesAtuais = baseProps.Concat(derivedProps).ToArray();
            }
            else _propriedadesAtuais = typeof(T).GetProperties();

            _dataGridView.Columns.Clear();
            foreach (var prop in _propriedadesAtuais) _dataGridView.Columns.Add(prop.Name, prop.Name);

            _dataGridView.RowCount = dados.Count;
            _dataGridView.Invalidate();
            AtualizarColunasDeFiltro();
        }

        private void DataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _dataGridView.RowCount) return;

            if (_listaAtualExibicao is System.Collections.IList lista)
            {
                var prop = _propriedadesAtuais.FirstOrDefault(p => p.Name == _dataGridView.Columns[e.ColumnIndex].Name);
                if (prop != null) e.Value = prop.GetValue(lista[e.RowIndex]);
            }
            else if (_dadosLivres != null)
            {
                int oIdx = _colunasLivres.IndexOf(_dataGridView.Columns[e.ColumnIndex].Name);
                var linha = _dadosLivres[e.RowIndex];
                if (oIdx >= 0 && oIdx < linha.Length) e.Value = linha[oIdx];
            }
        }

        private void DataGridView_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _dataGridView.RowCount) return;

            if (_listaAtualExibicao is System.Collections.IList lista)
            {
                var prop = _propriedadesAtuais.FirstOrDefault(p => p.Name == _dataGridView.Columns[e.ColumnIndex].Name);
                if (prop != null)
                {
                    try { prop.SetValue(lista[e.RowIndex], Convert.ChangeType(e.Value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType)); } catch { }
                }
            }
            else if (_dadosLivres != null)
            {
                int oIdx = _colunasLivres.IndexOf(_dataGridView.Columns[e.ColumnIndex].Name);
                var linha = _dadosLivres[e.RowIndex];
                if (oIdx >= linha.Length) { Array.Resize(ref linha, oIdx + 1); _dadosLivres[e.RowIndex] = linha; }
                if (oIdx >= 0) linha[oIdx] = e.Value?.ToString() ?? "";
            }
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _dataGridView.SelectedRows.Count > 0)
            {
                var indices = _dataGridView.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).OrderByDescending(i => i).ToList();
                if (_listaAtualExibicao is System.Collections.IList lista)
                {
                    foreach (int idx in indices) lista.RemoveAt(idx);
                    _dataGridView.RowCount = lista.Count;
                }
                else if (_dadosLivres != null)
                {
                    foreach (int idx in indices) _dadosLivres.RemoveAt(idx);
                    _dataGridView.RowCount = _dadosLivres.Count;
                }
                _dataGridView.Invalidate();
            }
        }

        private void DataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();

                var itemRenomear = new ToolStripMenuItem("Renomear Coluna");
                itemRenomear.Click += (s, args) => {
                    string novoNome = PromptTexto("Novo nome da coluna:", _dataGridView.Columns[e.ColumnIndex].HeaderText);
                    if (!string.IsNullOrWhiteSpace(novoNome)) { AvisarBloqueioFluxo(); _dataGridView.Columns[e.ColumnIndex].HeaderText = novoNome; AtualizarColunasDeFiltro(); }
                };

                var itemRemover = new ToolStripMenuItem("Excluir Coluna (Ocultar)");
                itemRemover.Click += (s, args) => {
                    AvisarBloqueioFluxo();
                    _dataGridView.Columns.RemoveAt(e.ColumnIndex); // Excluir a visualização é suficiente para o novo ExportarMatriz
                    AtualizarColunasDeFiltro();
                };

                menu.Items.Add(itemRenomear); menu.Items.Add(itemRemover);
                menu.Show(Cursor.Position);
            }
        }

        private void DataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();
            var font = new Font(grid.Font.FontFamily, grid.Font.Size, FontStyle.Regular);
            var centerFormat = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        // --- UTILITÁRIOS E INTERFACE ---

        private void AvisarBloqueioFluxo()
        {
            if (!_modoEdicaoLivre)
            {
                MessageBox.Show("Modificar colunas bloqueia a geração automática oficial.\nO programa alternará para MODO LIVRE (mantendo suas edições, permitindo salvar).", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                HabilitarModoLivre(true);
            }
        }

        private void HabilitarModoLivre(bool habilitar)
        {
            _modoEdicaoLivre = habilitar;
            foreach (Control btn in _panelBotoesFluxo.Controls) { if (btn is Button b && b.Name.StartsWith("btnFluxo")) b.Enabled = !habilitar; }
            if (habilitar) _nomeArquivoSugerido = "ArquivoEditado.csv";
        }

        private void AtualizarColunasDeFiltro()
        {
            _cmbColunaData.Items.Clear();
            foreach (DataGridViewColumn col in _dataGridView.Columns)
            {
                if (col.HeaderText.ToUpper().Contains("DATA")) _cmbColunaData.Items.Add(col.HeaderText);
            }
            if (_cmbColunaData.Items.Count > 0) _cmbColunaData.SelectedIndex = 0;
        }

        private void TravarInterface(bool processando)
        {
            _panelBotoesFluxo.Enabled = !processando;
            if (!processando) { _progressBar.Value = 0; _lblStatus.Text = "Pronto. Linhas Carregadas: " + (_dataGridView.RowCount).ToString("N0"); }
        }

        private string PromptTexto(string titulo, string valorPadrao)
        {
            Form prompt = new Form() { Width = 300, Height = 150, Text = titulo, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent };
            TextBox textBox = new TextBox() { Left = 20, Top = 20, Width = 240, Text = valorPadrao };
            Button confirmation = new Button() { Text = "Ok", Left = 160, Width = 100, Top = 60, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation); prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        private int SolicitarQuantidade(string titulo, int maximoGeral, int anterior)
        {
            string aviso = anterior > 0 ? $"O arquivo base possui {anterior:N0} linhas.\n" : "";
            Form prompt = new Form() { Width = 400, Height = 200, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Definir Quantidade", StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 10, Text = $"{aviso}{titulo}\n(Insira um valor entre 1 e {maximoGeral:N0}):", Width = 350, Height = 40 };
            TextBox textBox = new TextBox() { Left = 20, Top = 60, Width = 340, Text = maximoGeral.ToString() };
            Button confirmation = new Button() { Text = "Ok", Left = 260, Width = 100, Top = 100, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel); prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation); prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                if (int.TryParse(textBox.Text.Replace(".", ""), out int result) && result > 0 && result <= maximoGeral) return result;
                MessageBox.Show($"Insira um número válido entre 1 e {maximoGeral:N0}.");
            }
            return 0;
        }

        private void ConfigurarInterface()
        {
            this.Text = "Gerador e Editor de CSV - Alta Performance";
            this.Size = new Size(1250, 750);
            this.StartPosition = FormStartPosition.CenterScreen;

            _panelBotoesFluxo = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(10), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            Button btnPassantes = new Button { Name = "btnFluxo1", Text = "1. Gerar PASSANTES", Width = 220, Height = 40 }; btnPassantes.Click += BtnGerarPassantes_Click;
            Button btnInadimplentes = new Button { Name = "btnFluxo2", Text = "2. Gerar INADIMPLENTES", Width = 220, Height = 40 }; btnInadimplentes.Click += BtnGerarInadimplentes_Click;
            Button btnDaerNaoFiltrado = new Button { Name = "btnFluxo3", Text = "3. Gerar DAER Não Filtrado", Width = 220, Height = 40 }; btnDaerNaoFiltrado.Click += BtnGerarDaerNaoFiltrado_Click;
            Button btnRejeitados = new Button { Name = "btnFluxo4", Text = "4. Gerar REJEITADOS DAER", Width = 220, Height = 40 }; btnRejeitados.Click += BtnGerarRejeitados_Click;
            Button btnDaerFinal = new Button { Name = "btnFluxo5", Text = "5. Gerar DAER (Final)", Width = 220, Height = 40 }; btnDaerFinal.Click += BtnGerarDaerFinal_Click;
            Button btnPagamentos = new Button { Name = "btnFluxo6", Text = "6. Gerar PAGAMENTOS", Width = 220, Height = 40 }; btnPagamentos.Click += BtnGerarPagamentos_Click;
            _chkColunasVazias = new CheckBox { Text = "Gerar Pagamentos Vazios", Width = 220, Margin = new Padding(0, 10, 0, 5) };
            Button btnPreencherPagamentos = new Button { Text = "Preencher Pagamentos Vazios", Width = 220, Height = 35 }; btnPreencherPagamentos.Click += BtnPreencherPagamentos_Click;

            _panelBotoesFluxo.Controls.AddRange(new Control[] { btnPassantes, btnInadimplentes, btnDaerNaoFiltrado, btnRejeitados, btnDaerFinal, btnPagamentos, _chkColunasVazias, btnPreencherPagamentos });

            Panel panelTop = new Panel { Dock = DockStyle.Top, Height = 80, BorderStyle = BorderStyle.FixedSingle };
            Button btnImportar = new Button { Text = "Importar CSV", Width = 120, Height = 35, Left = 10, Top = 10 }; btnImportar.Click += BtnImportar_Click;
            Button btnExportar = new Button { Text = "Salvar CSV (Exportar)", Width = 150, Height = 35, Left = 140, Top = 10 }; btnExportar.Click += BtnExportar_Click;
            _chkComCabecalho = new CheckBox { Text = "Exportar c/ Cabeçalho", Checked = true, Left = 300, Top = 18, Width = 150 };

            GroupBox grpRecorte = new GroupBox { Text = "Recorte por Período", Left = 480, Top = 5, Width = 550, Height = 65 };
            _cmbColunaData = new ComboBox { Left = 10, Top = 25, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _dtpInicio = new DateTimePicker { Left = 150, Top = 25, Width = 110, Format = DateTimePickerFormat.Short };
            _dtpFim = new DateTimePicker { Left = 270, Top = 25, Width = 110, Format = DateTimePickerFormat.Short };
            Button btnFiltrar = new Button { Text = "Aplicar Recorte", Left = 390, Top = 23, Width = 130, Height = 28 }; btnFiltrar.Click += BtnFiltrar_Click;

            grpRecorte.Controls.AddRange(new Control[] { _cmbColunaData, _dtpInicio, _dtpFim, btnFiltrar });
            panelTop.Controls.AddRange(new Control[] { btnImportar, btnExportar, _chkComCabecalho, grpRecorte });

            Panel panelRodape = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            _progressBar = new ProgressBar { Left = 10, Top = 10, Width = 400, Height = 20 };
            _lblStatus = new Label { Left = 420, Top = 12, Width = 600, Text = "Aguardando ação..." };
            panelRodape.Controls.AddRange(new Control[] { _progressBar, _lblStatus });

            _dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                BackgroundColor = Color.White,
                RowHeadersWidth = 60
            };

            _dataGridView.CellValueNeeded += DataGridView_CellValueNeeded;
            _dataGridView.CellValuePushed += DataGridView_CellValuePushed;
            _dataGridView.KeyDown += DataGridView_KeyDown;
            _dataGridView.RowPostPaint += DataGridView_RowPostPaint;
            _dataGridView.ColumnHeaderMouseClick += DataGridView_ColumnHeaderMouseClick;

            this.Controls.Add(_dataGridView);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelRodape);
            this.Controls.Add(_panelBotoesFluxo);
        }
    }
}