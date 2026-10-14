using System.Linq;
using System.Numerics;

namespace Esquecidos.Nucleo.Simulacao
{
    public sealed partial class Mundo
    {
        public ObjetoDeCena AdicionarObjeto(ObjetoDeCena o)
        {
            if (o.Id == 0) o.Id = NovoId();
            Objetos.Add(o);
            AplicarBloqueio(o);
            return o;
        }

        public ObjetoDeCena Objeto(int id) => Objetos.FirstOrDefault(o => o.Id == id);

        private void AplicarBloqueio(ObjetoDeCena o)
        {
            bool deve = o.Bloqueando;
            if (deve == o.BloqueioAplicado) return;
            Grade.Bloquear(o.Bloqueio, deve ? 1 : -1);
            o.BloqueioAplicado = deve;
            if (deve) DesalojarDe(o.Bloqueio);
        }

        /// <summary>Quem estava onde um muro acabou de subir e jogado para o livre mais perto.</summary>
        private void DesalojarDe(Retangulo r)
        {
            foreach (Entidade e in Entidades)
            {
                if (!r.Contem(e.Pos) && Grade.FolgaEm(e.Pos) >= e.Raio * 0.5f) continue;
                if (Grade.LivreMaisProximo(e.Pos, e.Raio, out Vector2 livre, 40)) e.Pos = livre;
                e.Caminho = null;
            }
            foreach (ItemNoChao l in Loot)
                if (r.Contem(l.Pos) && Grade.LivreMaisProximo(l.Pos, 0.3f, out Vector2 livre)) l.Pos = livre;
        }

        /// <summary>Alguem esta em cima da area do bloqueio? Um muro nao sobe em cima do jogador.</summary>
        public bool AreaOcupada(Retangulo r, float margem = 0.2f)
        {
            var maior = new Retangulo(r.X0 - margem, r.Z0 - margem, r.X1 + margem, r.Z1 + margem);
            return Entidades.Any(e => e.Vivo && e.Faccao == Faccao.Jogador && maior.Contem(e.Pos));
        }

        /// <summary>
        /// Muda o estado de um objeto e propaga: alavanca alterna os alvos,
        /// placa de pressao deixa os alvos iguais a ela enquanto alguem pisa.
        /// </summary>
        public void DefinirLigado(ObjetoDeCena o, bool ligado)
        {
            if (o.Ligado == ligado) return;
            o.Ligado = ligado;
            AplicarBloqueio(o);
            Despachante.Despachar(new NotObjetoMudou(o));
            if (o.Tipo == TipoObjeto.Alavanca)
            {
                foreach (int id in o.Alvos)
                {
                    ObjetoDeCena alvo = Objeto(id);
                    if (alvo == null) continue;
                    if (alvo.Dependencias.Count > 0) DefinirLigado(alvo, alvo.Dependencias.All(d => Objeto(d)?.Ligado == true));
                    else DefinirLigado(alvo, !alvo.Ligado);
                }
            }
            else if (o.Tipo == TipoObjeto.Placa)
            {
                foreach (int id in o.Alvos)
                {
                    ObjetoDeCena alvo = Objeto(id);
                    if (alvo != null) DefinirLigado(alvo, ligado);
                }
            }
        }

        public void Interagir(Entidade quem, ObjetoDeCena o)
        {
            if (!o.Interativo) return;
            Despachante.Despachar(new NotInteracao(quem, o));
        }

        private void AtualizarObjetos()
        {
            foreach (ObjetoDeCena o in Objetos)
            {
                switch (o.Tipo)
                {
                    case TipoObjeto.Placa:
                    {
                        bool pisada = Entidades.Any(e => e.Vivo && !e.Voa && Vector2.Distance(e.Pos, o.Pos) <= o.RaioInteracao);
                        if (pisada != o.Ligado) DefinirLigado(o, pisada);
                        break;
                    }
                    case TipoObjeto.Gatilho:
                    case TipoObjeto.Saida:
                    {
                        if (Jogador == null || !Jogador.Vivo) break;
                        bool dentro = Vector2.Distance(Jogador.Pos, o.Pos) <= o.RaioInteracao;
                        if (dentro && !o.PisandoAgora && !(o.UmaVez && o.Usado))
                            Despachante.Despachar(new NotObjetoPisado(o, Jogador));
                        o.PisandoAgora = dentro;
                        break;
                    }
                }
            }
        }
    }
}
