using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSV_GeradorEEditor.Models;

namespace CSV_GeradorEEditor.Services
{
    public class CsvService
    {
        public void ExportarMatriz(List<string> cabecalhos, IEnumerable<string[]> linhas, string caminho, bool incluirCabecalho)
        {
            using (var writer = new StreamWriter(caminho, false, Encoding.UTF8))
            {
                if (incluirCabecalho && cabecalhos != null && cabecalhos.Count > 0)
                {
                    writer.WriteLine(string.Join(",", cabecalhos.Select(RemoverAcentos)));
                }

                foreach (var linha in linhas)
                {
                    writer.WriteLine(string.Join(",", linha.Select(RemoverAcentos)));
                }
            }
        }

        public void ImportarModoLivre(string caminho, out List<string> colunas, out List<string[]> dados)
        {
            var linhas = File.ReadAllLines(caminho, Encoding.UTF8);
            colunas = new List<string>();
            dados = new List<string[]>();

            if (linhas.Length == 0) return;

            // Sem invenções: cria colunas genéricas baseadas na quantidade de itens da linha 1
            int qtdColunas = linhas[0].Split(',').Length;
            for (int i = 0; i < qtdColunas; i++)
            {
                colunas.Add((i + 1).ToString());
            }

            // A linha 1 não é especial. Tudo é dado.
            for (int i = 0; i < linhas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(linhas[i])) continue;
                dados.Add(linhas[i].Split(','));
            }
        }

        public List<RegistroPassante> ImportarTipadoPassante(string caminho)
        {
            var lista = new List<RegistroPassante>();
            var linhas = File.ReadAllLines(caminho, Encoding.UTF8);

            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                // Se a linha contiver as palavras abaixo, aí sim é o título antigo e nós ignoramos
                if (linha.ToUpper().Contains("PLACA") && linha.ToUpper().Contains("PORTICO")) continue;

                var colunas = linha.Split(',');
                lista.Add(new RegistroPassante
                {
                    Placa = colunas.Length > 0 ? colunas[0] : "",
                    Data = colunas.Length > 1 ? colunas[1] : "",
                    Hora = colunas.Length > 2 ? colunas[2] : "",
                    Portico = colunas.Length > 3 ? colunas[3] : "",
                    Categoria = colunas.Length > 4 && int.TryParse(colunas[4], out int cat) ? cat : 1,
                    Sentido = colunas.Length > 5 ? colunas[5] : "",
                    Tarifa = colunas.Length > 6 && double.TryParse(colunas[6], NumberStyles.Any, CultureInfo.InvariantCulture, out double tar) ? tar : 0
                });
            }
            return lista;
        }

        public List<RegistroPagamento> ImportarTipadoPagamento(string caminho)
        {
            var lista = new List<RegistroPagamento>();
            var linhas = File.ReadAllLines(caminho, Encoding.UTF8);

            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (linha.ToUpper().Contains("PLACA") && linha.ToUpper().Contains("PORTICO")) continue;

                var colunas = linha.Split(',');
                var pag = new RegistroPagamento
                {
                    Placa = colunas.Length > 0 ? colunas[0] : "",
                    Data = colunas.Length > 1 ? colunas[1] : "",
                    Hora = colunas.Length > 2 ? colunas[2] : "",
                    Portico = colunas.Length > 3 ? colunas[3] : "",
                    Categoria = colunas.Length > 4 && int.TryParse(colunas[4], out int cat) ? cat : 1,
                    Sentido = colunas.Length > 5 ? colunas[5] : "",
                    Tarifa = colunas.Length > 6 && double.TryParse(colunas[6], NumberStyles.Any, CultureInfo.InvariantCulture, out double tar) ? tar : 0
                };
                if (colunas.Length > 7)
                {
                    pag.DataPagamento = colunas[7];
                    pag.HoraPagamento = colunas.Length > 8 ? colunas[8] : "";
                    pag.Acrescimo = colunas.Length > 9 && double.TryParse(colunas[9], NumberStyles.Any, CultureInfo.InvariantCulture, out double acr) ? acr : (double?)null;
                    pag.ValorTotal = colunas.Length > 10 && double.TryParse(colunas[10], NumberStyles.Any, CultureInfo.InvariantCulture, out double tot) ? tot : (double?)null;
                }
                lista.Add(pag);
            }
            return lista;
        }

        private string RemoverAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto;
            texto = texto.ToUpper();
            var textoNormalizado = texto.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var c in textoNormalizado)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) builder.Append(c);
            }
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}