using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSV_GeradorEEditor.Models;

namespace CSV_GeradorEEditor.Services
{
    public class GeradorDadosService
    {
        // Random não é thread-safe. Usamos ThreadLocal para que cada núcleo da CPU tenha seu próprio gerador.
        private static readonly ThreadLocal<Random> ThreadRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        private static readonly string[] Porticos = { "IPE", "ANTONIO PRADO", "CARLOS BARBOSA", "FARROUPILHA", "SAO SEBASTIAO DO CAI", "CAPELA DE SANTANA" };
        private static readonly string[] Sentidos = { "SUL", "NORTE" };

        public List<RegistroPassante> GerarPassantes(int quantidade, IProgress<int> progresso)
        {
            // Pré-alocação de array para evitar colisões de memória entre threads
            var array = new RegistroPassante[quantidade];
            DateTime dataInicio = new DateTime(2023, 12, 15);
            DateTime dataFim = new DateTime(2024, 12, 31);
            int rangeDias = (dataFim - dataInicio).Days;

            int contador = 0;

            Parallel.For(0, quantidade, i =>
            {
                var rnd = ThreadRandom.Value;
                var dataAleatoria = dataInicio.AddDays(rnd.Next(rangeDias));

                array[i] = new RegistroPassante
                {
                    Placa = GerarPlaca(rnd),
                    Data = dataAleatoria.ToString("dd/MM/yyyy"),
                    Hora = $"{rnd.Next(0, 24):D2}:{rnd.Next(0, 60):D2}:{rnd.Next(0, 60):D2}",
                    Portico = Porticos[rnd.Next(Porticos.Length)],
                    Categoria = rnd.Next(1, 15),
                    Sentido = Sentidos[rnd.Next(Sentidos.Length)],
                    Tarifa = Math.Round(rnd.NextDouble() * (100.0 - 10.0) + 10.0, 2)
                };

                // Atualiza a barra de progresso a cada 10.000 registros para não afogar a interface gráfica
                int atual = Interlocked.Increment(ref contador);
                if (atual % 10000 == 0 || atual == quantidade)
                {
                    progresso?.Report((atual * 100) / quantidade);
                }
            });

            return array.ToList();
        }

        public List<T> GerarSubconjunto<T>(List<T> baseLista, int quantidadeDesejada)
        {
            if (quantidadeDesejada >= baseLista.Count) return new List<T>(baseLista);
            // Uso de PLINQ (AsParallel) para ordenação multicore
            return baseLista.AsParallel().OrderBy(x => ThreadRandom.Value.Next()).Take(quantidadeDesejada).ToList();
        }

        public List<RegistroPagamento> GerarPagamentos(List<RegistroPassante> baseDaer, int quantidade, bool colunasVazias, IProgress<int> progresso)
        {
            var subconjunto = GerarSubconjunto(baseDaer, quantidade);
            var arrayPagamentos = new RegistroPagamento[quantidade];
            int contador = 0;

            Parallel.For(0, quantidade, i =>
            {
                var rnd = ThreadRandom.Value;
                var registro = subconjunto[i];

                var pag = new RegistroPagamento
                {
                    Placa = registro.Placa,
                    Data = registro.Data,
                    Hora = registro.Hora,
                    Portico = registro.Portico,
                    Categoria = registro.Categoria,
                    Sentido = registro.Sentido,
                    Tarifa = registro.Tarifa
                };

                if (!colunasVazias)
                {
                    DateTime dataOrigem = DateTime.TryParseExact(registro.Data, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsed) ? parsed : DateTime.Now;
                    DateTime dataPag = dataOrigem.AddDays(rnd.Next(30, 730));

                    pag.DataPagamento = dataPag.ToString("dd/MM/yyyy");
                    pag.HoraPagamento = $"{rnd.Next(0, 24):D2}:{rnd.Next(0, 60):D2}:{rnd.Next(0, 60):D2}";
                    pag.Acrescimo = Math.Round(rnd.NextDouble() * (100.0 - 10.0) + 10.0, 2);
                    pag.ValorTotal = Math.Round(registro.Tarifa + pag.Acrescimo.Value, 2);
                }

                arrayPagamentos[i] = pag;

                int atual = Interlocked.Increment(ref contador);
                if (atual % 10000 == 0 || atual == quantidade)
                {
                    progresso?.Report((atual * 100) / quantidade);
                }
            });

            return arrayPagamentos.ToList();
        }

        private string GerarPlaca(Random rnd)
        {
            const string letras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numeros = "0123456789";
            string pLetras = new string(Enumerable.Repeat(letras, 3).Select(s => s[rnd.Next(s.Length)]).ToArray());
            string pNumeros = new string(Enumerable.Repeat(numeros, 4).Select(s => s[rnd.Next(s.Length)]).ToArray());
            return pLetras + pNumeros;
        }

        public void PreencherPagamentosVazios(List<RegistroPagamento> pagamentos, IProgress<int> progresso)
        {
            int contador = 0;
            Parallel.For(0, pagamentos.Count, i =>
            {
                var pag = pagamentos[i];
                if (string.IsNullOrWhiteSpace(pag.DataPagamento))
                {
                    var rnd = ThreadRandom.Value;
                    DateTime dataOrigem = DateTime.TryParseExact(pag.Data, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsed) ? parsed : DateTime.Now;
                    DateTime dataPag = dataOrigem.AddDays(rnd.Next(30, 730));

                    pag.DataPagamento = dataPag.ToString("dd/MM/yyyy");
                    pag.HoraPagamento = $"{rnd.Next(0, 24):D2}:{rnd.Next(0, 60):D2}:{rnd.Next(0, 60):D2}";
                    pag.Acrescimo = Math.Round(rnd.NextDouble() * (100.0 - 10.0) + 10.0, 2);
                    pag.ValorTotal = Math.Round(pag.Tarifa + pag.Acrescimo.Value, 2);
                }

                int atual = Interlocked.Increment(ref contador);
                if (atual % 10000 == 0 || atual == pagamentos.Count)
                {
                    progresso?.Report((atual * 100) / pagamentos.Count);
                }
            });
        }
    }
}