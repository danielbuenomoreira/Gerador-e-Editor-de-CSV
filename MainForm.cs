using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        // Instâncias dos serviços
        private GeradorDadosService _geradorService;
        private ProcessadorArquivosService _processadorService;
        private CsvService _csvService;

        // Armazenamento em memória do fluxo padrão
        private List<RegistroPassante> _passantes;
        private List<RegistroPassante> _inadimplentes;
        private List<RegistroPassante> _daerNaoFiltrado;
        private List<RegistroPassante> _rejeitados;
        private List<RegistroPassante> _daer;
        private List<RegistroPagamento> _pagamentos;

        // Armazenamento para o Modo de Edição Livre
        private List<string[]> _dadosLivres;
        private List<string> _colunasLivres;

        // Estado e Controles
        private object _listaAtualExibicao;
        private PropertyInfo[] _propriedadesAtuais;
        private string _nomeArquivoSugerido = "PASSANTES.csv";
        private bool _modoEdicaoLivre = false;

        private DataGridView _dataGridView;
        private CheckBox _chkComCabecalho;
        private CheckBox _chkColunasVazias;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private FlowLayoutPanel _panelBotoesFluxo;

        private GroupBox _grpRecorte;
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

        // --- SISTEMA ---

        private void BtnReset_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Tem certeza que deseja voltar ao estado inicial? Todos os dados não salvos na tela atual e em etapas anteriores serão perdidos da memória.", "Confirmar Reinício", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _passantes = null;
                _inadimplentes = null;
                _daerNaoFiltrado = null;
                _rejeitados = null;
                _daer = null;
                _pagamentos = null;

                _dadosLivres = null;
                _colunasLivres = null;

                _listaAtualExibicao = null;
                _propriedadesAtuais = null;

                _dataGridView.Columns.Clear();
                _dataGridView.RowCount = 0;
                _dataGridView.Invalidate();

                _cmbColunaData.Items.Clear();
                _chkColunasVazias.Checked = false;
                _chkComCabecalho.Checked = true;

                HabilitarModoLivre(false);
                _nomeArquivoSugerido = "PASSANTES.csv";

                TravarInterface(false);
                _lblStatus.Text = "Sistema reiniciado. Aguardando novos dados.";
            }
        }

        // --- MÉTODOS DE GERAÇÃO (FLUXO PRINCIPAL) ---

        private async void BtnGerarPassantes_Click(object sender, EventArgs e)
        {
            int qtd = SolicitarQuantidade("Gerar PASSANTES (Max: 10.000.000)", 10000000, 0);
            if (qtd <= 0) return;

            TravarInterface(true);
            _lblStatus.Text = "Utilizando threads para gerar dados...";
            var progresso = new Progress<int>(p => _progressBar.Value = p);

            _passantes = await Task.Run(() => _geradorService.GerarPassantes(qtd, progresso));
            ConfigurarExibicao("PASSANTES.csv", _passantes);
            TravarInterface(false);
        }

        private async void BtnGerarInadimplentes_Click(object sender, EventArgs e)
        {
            if (_passantes == null) { MessageBox.Show("Gere ou Importe os PASSANTES primeiro."); return; }
            int qtd = SolicitarQuantidade("INADIMPLENTES a gerar:", _passantes.Count, _passantes.Count);
            if (qtd <= 0) return;

            TravarInterface(true);
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
            if (_daerNaoFiltrado == null) { MessageBox.Show("Gere o DAER Não Filtrado primeiro."); return; }
            int qtd = SolicitarQuantidade("REJEITADOS DAER a gerar:", _daerNaoFiltrado.Count, _daerNaoFiltrado.Count);
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
            if (_daer == null) { MessageBox.Show("Gere o DAER final primeiro."); return; }
            int qtd = SolicitarQuantidade("PAGAMENTOS a gerar:", _daer.Count, _daer.Count);
            if (qtd <= 0) return;

            TravarInterface(true);
            var progresso = new Progress<int>(p => _progressBar.Value = p);
            _pagamentos = await Task.Run(() => _geradorService.GerarPagamentos(_daer, qtd, _chkColunasVazias.Checked, progresso));
            ConfigurarExibicao("PAGAMENTOS.csv", _pagamentos);
            TravarInterface(false);
        }

        private async void BtnPreencherPagamentos_Click(object sender, EventArgs e)
        {
            if (_pagamentos == null || _modoEdicaoLivre) { MessageBox.Show("Opção válida apenas no fluxo original de PAGAMENTOS."); return; }
            TravarInterface(true);
            var progresso = new Progress<int>(p => _progressBar.Value = p);
            await Task.Run(() => _geradorService.PreencherPagamentosVazios(_pagamentos, progresso));
            _dataGridView.Invalidate();
            TravarInterface(false);
        }

        // --- IMPORTAÇÃO E EXPORTAÇÃO ---

        private async void BtnImportar_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "CSV|*.csv" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Form dialogo = new Form() { Width = 450, Height = 250, Text = "Selecione o Destino dos Dados", StartPosition = FormStartPosition.CenterScreen };
                    ComboBox cmbFluxo = new ComboBox() { Left = 20, Top = 25, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
                    cmbFluxo.Items.AddRange(new string[] { "1 - PASSANTES", "2 - INADIMPLENTES", "3 - DAER Não Filtrado", "4 - REJEITADOS DAER", "5 - DAER", "6 - PAGAMENTOS", "7 - MODO LIVRE (Edição Manual)" });
                    cmbFluxo.SelectedIndex = 6;
                    Button btnConfirmar = new Button() { Text = "Confirmar Importação", Left = 130, Top = 100, Width = 180, Height = 40, DialogResult = DialogResult.OK };
                    dialogo.Controls.AddRange(new Control[] { cmbFluxo, btnConfirmar });

                    if (dialogo.ShowDialog() == DialogResult.OK)
                    {
                        TravarInterface(true);
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
                        catch (Exception ex) { MessageBox.Show("Erro na estrutura do arquivo: " + ex.Message); }
                        TravarInterface(false);
                    }
                }
            }
        }

        private async void BtnExportar_Click(object sender, EventArgs e)
        {
            if (_listaAtualExibicao == null && _dadosLivres == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "CSV|*.csv", FileName = _nomeArquivoSugerido })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    bool usarCabecalho = _chkComCabecalho.Checked;
                    string caminho = sfd.FileName;
                    TravarInterface(true);

                    var colunasGrid = _dataGridView.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).ToList();
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
                                            linha[i] = val is double d ? d.ToString(CultureInfo.InvariantCulture) : val?.ToString() ?? "";
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
                                        int idx = indexMap[i];
                                        linha[i] = (idx >= 0 && idx < item.Length) ? item[idx] : "";
                                    }
                                    yield return linha;
                                }
                            }
                        }
                        _csvService.ExportarMatriz(cabecalhos, ObterLinhas(), caminho, usarCabecalho);
                    });

                    TravarInterface(false);
                    MessageBox.Show("Arquivo exportado com sucesso.");
                }
            }
        }

        // --- FILTRAGEM E RECORTE (APENAS FLUXO PADRÃO) ---

        private async void BtnFiltrar_Click(object sender, EventArgs e)
        {
            if (_modoEdicaoLivre) return; // Segurança extra
            if (_listaAtualExibicao == null) return;
            if (_cmbColunaData.SelectedItem == null) { MessageBox.Show("Selecione a coluna de data."); return; }

            string nomeColunaGrid = _cmbColunaData.SelectedItem.ToString();
            string nomeColunaOriginal = _dataGridView.Columns.Cast<DataGridViewColumn>().First(c => c.HeaderText == nomeColunaGrid).Name;

            if (!ValidarSeColunaEData(nomeColunaOriginal))
            {
                MessageBox.Show("Ops! Essa coluna não é do tipo Data.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DateTime inicio = _dtpInicio.Value.Date;
            DateTime fim = _dtpFim.Value.Date;

            TravarInterface(true);

            List<RegistroPagamento> pagamentosFiltrados = null;
            List<RegistroPassante> passantesFiltrados = null;
            System.Collections.IList listaFiltradaExibicao = null;

            await Task.Run(() =>
            {
                if (_listaAtualExibicao is List<RegistroPagamento> lpag)
                {
                    pagamentosFiltrados = _processadorService.FiltrarPorData(lpag, nomeColunaOriginal, inicio, fim);
                    listaFiltradaExibicao = pagamentosFiltrados;
                }
                else if (_listaAtualExibicao is List<RegistroPassante> lpass)
                {
                    passantesFiltrados = _processadorService.FiltrarPorData(lpass, nomeColunaOriginal, inicio, fim);
                    listaFiltradaExibicao = passantesFiltrados;
                }
            });

            _listaAtualExibicao = listaFiltradaExibicao;
            if (pagamentosFiltrados != null) _pagamentos = pagamentosFiltrados;
            if (passantesFiltrados != null) AtualizarReferenciaFluxo(passantesFiltrados);
            _dataGridView.RowCount = listaFiltradaExibicao.Count;

            _dataGridView.Invalidate();
            TravarInterface(false);
            MessageBox.Show("Recorte aplicado com sucesso.", "Filtro");
        }

        private bool ValidarSeColunaEData(string nomeColunaOriginal)
        {
            if (_listaAtualExibicao is System.Collections.IList lista)
            {
                if (lista.Count == 0) return true;
                var prop = _propriedadesAtuais.FirstOrDefault(p => p.Name == nomeColunaOriginal);
                if (prop != null)
                {
                    foreach (var item in lista)
                    {
                        var val = prop.GetValue(item)?.ToString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            return DateTime.TryParseExact(val, "dd/MM/yyyy", null, DateTimeStyles.None, out _);
                        }
                    }
                }
            }
            return false;
        }

        private void AtualizarReferenciaFluxo(List<RegistroPassante> dados)
        {
            if (_nomeArquivoSugerido == "PASSANTES.csv") _passantes = dados;
            else if (_nomeArquivoSugerido == "INADIMPLENTES.csv") _inadimplentes = dados;
            else if (_nomeArquivoSugerido == "DAERnaoFiltrado.csv") _daerNaoFiltrado = dados;
            else if (_nomeArquivoSugerido == "REJEITADOSDAER.csv") _rejeitados = dados;
            else if (_nomeArquivoSugerido == "DAER.csv") _daer = dados;
        }

        // --- LÓGICA DA GRADE (VIRTUAL MODE) ---

        private void ConfigurarExibicao<T>(string nomeSugestao, List<T> dados)
        {
            _nomeArquivoSugerido = nomeSugestao;
            _listaAtualExibicao = dados;

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
            AtualizarContadorRodape();
        }

        private void ConfigurarExibicaoLivre()
        {
            _listaAtualExibicao = null;
            _dataGridView.Columns.Clear();
            foreach (var col in _colunasLivres) _dataGridView.Columns.Add(col, col);
            _dataGridView.RowCount = _dadosLivres.Count;
            _dataGridView.Invalidate();
            HabilitarModoLivre(true);
            AtualizarContadorRodape();
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
                if (oIdx >= 0 && oIdx < _dadosLivres[e.RowIndex].Length) e.Value = _dadosLivres[e.RowIndex][oIdx];
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
                if (oIdx >= 0) _dadosLivres[e.RowIndex][oIdx] = e.Value?.ToString() ?? "";
            }
        }

        private void ExcluirLinhasSelecionadas()
        {
            if (_dataGridView.SelectedRows.Count > 0)
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
                AtualizarContadorRodape();
            }
        }

        private void DataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) ExcluirLinhasSelecionadas();
        }

        private void DataGridView_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _dataGridView.ClearSelection();
                _dataGridView.Rows[e.RowIndex].Selected = true;
                ContextMenuStrip menu = new ContextMenuStrip();
                var itemExcluir = new ToolStripMenuItem("Excluir Linha(s)");
                itemExcluir.Click += (s, args) => ExcluirLinhasSelecionadas();
                menu.Items.Add(itemExcluir);
                menu.Show(Cursor.Position);
            }
        }

        private void DataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                var itemRenomear = new ToolStripMenuItem("Renomear Coluna (Tornará um Título)");
                itemRenomear.Click += (s, args) => {
                    string novoNome = PromptTexto("Novo nome:", _dataGridView.Columns[e.ColumnIndex].HeaderText);
                    if (!string.IsNullOrWhiteSpace(novoNome)) { AvisarBloqueioFluxo(); _dataGridView.Columns[e.ColumnIndex].HeaderText = novoNome; AtualizarColunasDeFiltro(); }
                };
                var itemRemover = new ToolStripMenuItem("Excluir Coluna");
                itemRemover.Click += (s, args) => { AvisarBloqueioFluxo(); _dataGridView.Columns.RemoveAt(e.ColumnIndex); AtualizarColunasDeFiltro(); };
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

        // --- UTILITÁRIOS ---

        private void AvisarBloqueioFluxo()
        {
            if (!_modoEdicaoLivre)
            {
                MessageBox.Show("Alterar a estrutura bloqueia a geração oficial das próximas etapas.\nEntrando em MODO LIVRE (você poderá editar e salvar normalmente).", "Aviso");
                HabilitarModoLivre(true);
            }
        }

        private void HabilitarModoLivre(bool habilitar)
        {
            _modoEdicaoLivre = habilitar;
            foreach (Control btn in _panelBotoesFluxo.Controls)
            {
                if (btn is Button b && b.Name.StartsWith("btnFluxo")) b.Enabled = !habilitar;
            }
            _grpRecorte.Enabled = !habilitar;
            if (habilitar) _nomeArquivoSugerido = "ArquivoEditado.csv";
        }

        private void AtualizarContadorRodape()
        {
            int totalLinhasArquivo = _modoEdicaoLivre ? _dataGridView.RowCount : _dataGridView.RowCount + 1;
            if (_dataGridView.RowCount == 0) totalLinhasArquivo = 0;
            _lblStatus.Text = $"Arquivo com {totalLinhasArquivo:N0} linhas no total";
        }

        private void TravarInterface(bool processando)
        {
            _panelBotoesFluxo.Enabled = !processando;
            if (!processando)
            {
                _progressBar.Value = 0;
                AtualizarContadorRodape();
            }
        }

        private void AtualizarColunasDeFiltro()
        {
            if (_modoEdicaoLivre) return;

            _cmbColunaData.Items.Clear();
            foreach (DataGridViewColumn col in _dataGridView.Columns)
            {
                _cmbColunaData.Items.Add(col.HeaderText);
            }

            if (_cmbColunaData.Items.Count > 0)
            {
                int indexSelecionado = 0;
                for (int i = 0; i < _cmbColunaData.Items.Count; i++)
                {
                    if (_cmbColunaData.Items[i].ToString().ToUpper().Contains("DATA"))
                    {
                        indexSelecionado = i;
                        break;
                    }
                }
                _cmbColunaData.SelectedIndex = indexSelecionado;
            }
        }

        private string PromptTexto(string titulo, string padrao)
        {
            Form f = new Form() { Width = 350, Height = 150, Text = titulo, StartPosition = FormStartPosition.CenterParent };
            TextBox t = new TextBox() { Left = 20, Top = 20, Width = 290, Text = padrao };
            Button b = new Button() { Text = "Ok", Left = 210, Top = 60, Width = 100, DialogResult = DialogResult.OK };
            f.Controls.Add(t); f.Controls.Add(b); f.AcceptButton = b;
            return f.ShowDialog() == DialogResult.OK ? t.Text : "";
        }

        private int SolicitarQuantidade(string titulo, int max, int anterior)
        {
            int maxCalculado = anterior > 0 ? anterior : max;
            string msg = anterior > 0 ? $"O arquivo base possui {anterior:N0} linhas.\n" : "";
            Form p = new Form() { Width = 480, Height = 250, Text = "Quantidade", StartPosition = FormStartPosition.CenterScreen };
            Label l = new Label() { Left = 20, Top = 15, Text = $"{msg}{titulo}\n(1 a {maxCalculado:N0}):", Width = 420, Height = 60 };
            TextBox t = new TextBox() { Left = 20, Top = 85, Width = 420, Text = maxCalculado.ToString() };
            Button c = new Button() { Text = "Ok", Left = 340, Top = 140, Width = 100, DialogResult = DialogResult.OK };
            p.Controls.Add(l); p.Controls.Add(t); p.Controls.Add(c); p.AcceptButton = c;
            if (p.ShowDialog() == DialogResult.OK && int.TryParse(t.Text.Replace(".", ""), out int res) && res > 0 && res <= maxCalculado) return res;
            return 0;
        }

        private void ConfigurarInterface()
        {
            this.Text = "Gerador e Editor de CSV - Alta Performance";
            this.Size = new Size(1250, 750);
            this.StartPosition = FormStartPosition.CenterScreen;

            _panelBotoesFluxo = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(10), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            string[] nomes = { "btnFluxo1|PASSANTES", "btnFluxo2|INADIMPLENTES", "btnFluxo3|DAER Não Filtrado", "btnFluxo4|REJEITADOS DAER", "btnFluxo5|DAER (Final)", "btnFluxo6|PAGAMENTOS" };
            foreach (var n in nomes)
            {
                var parts = n.Split('|');
                Button b = new Button { Name = parts[0], Text = parts[1], Width = 220, Height = 40 };
                if (parts[0].EndsWith("1")) b.Click += BtnGerarPassantes_Click;
                else if (parts[0].EndsWith("2")) b.Click += BtnGerarInadimplentes_Click;
                else if (parts[0].EndsWith("3")) b.Click += BtnGerarDaerNaoFiltrado_Click;
                else if (parts[0].EndsWith("4")) b.Click += BtnGerarRejeitados_Click;
                else if (parts[0].EndsWith("5")) b.Click += BtnGerarDaerFinal_Click;
                else b.Click += BtnGerarPagamentos_Click;
                _panelBotoesFluxo.Controls.Add(b);
            }
            _chkColunasVazias = new CheckBox { Text = "Gerar Pagamentos Vazios", Width = 220, Margin = new Padding(0, 10, 0, 5) };
            Button bPre = new Button { Text = "Preencher Colunas Vazias", Width = 220, Height = 35 }; bPre.Click += BtnPreencherPagamentos_Click;

            Button btnReset = new Button { Text = "🔄 Voltar ao Início (Limpar)", Width = 220, Height = 35, Margin = new Padding(0, 30, 0, 0) };
            btnReset.Click += BtnReset_Click;

            _panelBotoesFluxo.Controls.Add(_chkColunasVazias);
            _panelBotoesFluxo.Controls.Add(bPre);
            _panelBotoesFluxo.Controls.Add(btnReset);

            Panel pTop = new Panel { Dock = DockStyle.Top, Height = 80, BorderStyle = BorderStyle.FixedSingle };
            Button bImp = new Button { Text = "Importar CSV", Width = 110, Height = 35, Left = 10, Top = 10 }; bImp.Click += BtnImportar_Click;
            Button bExp = new Button { Text = "Salvar/Exportar", Width = 130, Height = 35, Left = 130, Top = 10 }; bExp.Click += BtnExportar_Click;
            _chkComCabecalho = new CheckBox { Text = "Exportar com a Linha nº 1 (Títulos)?", Checked = true, Left = 270, Top = 18, Width = 230 };

            _grpRecorte = new GroupBox { Text = "Recorte por Período", Left = 510, Top = 5, Width = 550, Height = 65 };
            _cmbColunaData = new ComboBox { Left = 10, Top = 25, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _dtpInicio = new DateTimePicker { Left = 150, Top = 25, Width = 110, Format = DateTimePickerFormat.Short };
            _dtpFim = new DateTimePicker { Left = 270, Top = 25, Width = 110, Format = DateTimePickerFormat.Short };
            Button bFil = new Button { Text = "Aplicar Recorte", Left = 390, Top = 23, Width = 130, Height = 28 }; bFil.Click += BtnFiltrar_Click;
            _grpRecorte.Controls.AddRange(new Control[] { _cmbColunaData, _dtpInicio, _dtpFim, bFil });

            pTop.Controls.AddRange(new Control[] { bImp, bExp, _chkComCabecalho, _grpRecorte });

            Panel pRod = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            _progressBar = new ProgressBar { Left = 10, Top = 10, Width = 400, Height = 20 };
            _lblStatus = new Label { Left = 420, Top = 12, Width = 600, Text = "Pronto." };
            pRod.Controls.AddRange(new Control[] { _progressBar, _lblStatus });

            _dataGridView = new DataGridView { Dock = DockStyle.Fill, VirtualMode = true, AllowUserToAddRows = false, EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2, BackgroundColor = Color.White, RowHeadersWidth = 60 };
            _dataGridView.CellValueNeeded += DataGridView_CellValueNeeded;
            _dataGridView.CellValuePushed += DataGridView_CellValuePushed;
            _dataGridView.KeyDown += DataGridView_KeyDown;
            _dataGridView.RowPostPaint += DataGridView_RowPostPaint;
            _dataGridView.ColumnHeaderMouseClick += DataGridView_ColumnHeaderMouseClick;
            _dataGridView.RowHeaderMouseClick += DataGridView_RowHeaderMouseClick;

            this.Controls.Add(_dataGridView);
            this.Controls.Add(pTop);
            this.Controls.Add(pRod);
            this.Controls.Add(_panelBotoesFluxo);
        }
    }
}