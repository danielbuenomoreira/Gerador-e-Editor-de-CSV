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

            // O cabeçalho é lido apenas como a primeira linha puramente visual
            var cabecalhosRaw = linhas[0].Split(',');
            for (int i = 0; i < cabecalhosRaw.Length; i++)
            {
                string colName = string.IsNullOrWhiteSpace(cabecalhosRaw[i]) ? $"Coluna_{i + 1}" : cabecalhosRaw[i];

                // Evita crash se o usuário importar CSV com colunas de mesmo nome
                int count = 1;
                string uniqueName = colName;
                while (colunas.Contains(uniqueName)) { uniqueName = $"{colName}_{count++}"; }
                colunas.Add(uniqueName);
            }

            // Os dados começam da linha 1 em diante
            for (int i = 1; i < linhas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(linhas[i])) continue;
                dados.Add(linhas[i].Split(','));
            }
        }

        public List<RegistroPassante> ImportarTipadoPassante(string caminho)
        {
            var lista = new List<RegistroPassante>();
            var linhas = File.ReadAllLines(caminho, Encoding.UTF8).Skip(1);

            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;
                var colunas = linha.Split(',');
                lista.Add(new RegistroPassante
                {
                    Placa = colunas[0],
                    Data = colunas[1],
                    Hora = colunas[2],
                    Portico = colunas[3],
                    Categoria = int.TryParse(colunas[4], out int cat) ? cat : 1,
                    Sentido = colunas[5],
                    Tarifa = double.TryParse(colunas[6], NumberStyles.Any, CultureInfo.InvariantCulture, out double tar) ? tar : 0
                });
            }
            return lista;
        }

        public List<RegistroPagamento> ImportarTipadoPagamento(string caminho)
        {
            var lista = new List<RegistroPagamento>();
            var linhas = File.ReadAllLines(caminho, Encoding.UTF8).Skip(1);

            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;
                var colunas = linha.Split(',');
                var pag = new RegistroPagamento
                {
                    Placa = colunas[0],
                    Data = colunas[1],
                    Hora = colunas[2],
                    Portico = colunas[3],
                    Categoria = int.TryParse(colunas[4], out int cat) ? cat : 1,
                    Sentido = colunas[5],
                    Tarifa = double.TryParse(colunas[6], NumberStyles.Any, CultureInfo.InvariantCulture, out double tar) ? tar : 0
                };
                if (colunas.Length > 7)
                {
                    pag.DataPagamento = colunas[7]; pag.HoraPagamento = colunas[8];
                    pag.Acrescimo = double.TryParse(colunas[9], NumberStyles.Any, CultureInfo.InvariantCulture, out double acr) ? acr : (double?)null;
                    pag.ValorTotal = double.TryParse(colunas[10], NumberStyles.Any, CultureInfo.InvariantCulture, out double tot) ? tot : (double?)null;
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