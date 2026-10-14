using System;
using UnityEngine;

namespace Esquecidos.Motor.Som
{
    /// <summary>Receita de uma trilha: tom, escala, andamento e quanto de cada instrumento.</summary>
    public sealed class TemaMusical
    {
        public string Nome = "";
        public int Tonica = 45;
        public int[] Escala = { 0, 3, 5, 7, 10 };
        public int[] Progressao = { 0, 3, 4, 2 };
        public float Bpm = 72;
        public float Drone = 0.5f;
        public float Pad = 0.5f;
        public float Dedilhado = 0.4f;
        public float Sino = 0.2f;
        public float Percussao = 0.5f;
        public float Brilho = 1f;
        public float Vento = 0.3f;
        public float Grilos;
        public float Metal;
    }

    /// <summary>
    /// Trilha generativa: compoe enquanto toca, no thread de audio. Drone de
    /// fundo, acordes longos, cordas dedilhadas (Karplus-Strong), sinos FM e
    /// uma percussao que so entra quando o combate esquenta. Por cima, o
    /// ambiente: vento, grilos de noite no Patio, metal batendo longe no
    /// labirinto. Nada se repete e nenhum arquivo de audio e usado.
    ///
    /// Tudo pre-alocado: o thread de audio nao pode alocar nem esperar.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MotorDeMusica : MonoBehaviour
    {
        // ---- controle (escrito no thread principal, lido no de audio) ----
        public volatile float Volume = 0.55f;
        public volatile float VolumeAmbiente = 0.6f;
        public volatile float IntensidadeAlvo;
        public volatile float NoiteAlvo;
        private volatile TemaMusical _pedido;

        private TemaMusical _tema;
        private TemaMusical _anterior;
        private float _mistura = 1;
        private int _taxa = 48000;
        private uint _rng = 2463534242;

        private float _intensidade;
        private float _noite;
        private long _amostra;
        private int _passo;
        private int _amostrasPorPasso;
        private int _contaPasso;
        private int _acorde;
        private int _ultimaNota = 2;

        // drone
        private double _fd1, _fd2, _fd3, _lfo;
        private float _lpDrone;

        // pads
        private const int Pads = 6;
        private readonly float[] _padFreq = new float[Pads];
        private readonly double[] _padF1 = new double[Pads], _padF2 = new double[Pads], _padF3 = new double[Pads];
        private readonly float[] _padT = new float[Pads], _padDur = new float[Pads], _padAmp = new float[Pads];

        // dedilhado (Karplus-Strong)
        private const int Cordas = 8;
        private const int MaxCorda = 2048;
        private readonly float[][] _corda = new float[Cordas][];
        private readonly int[] _cordaPos = new int[Cordas], _cordaLen = new int[Cordas];
        private readonly float[] _cordaAmp = new float[Cordas];
        private readonly bool[] _cordaViva = new bool[Cordas];
        private int _proximaCorda;

        // sinos FM
        private const int Sinos = 4;
        private readonly float[] _sinoFreq = new float[Sinos], _sinoT = new float[Sinos], _sinoAmp = new float[Sinos];
        private readonly double[] _sinoFc = new double[Sinos], _sinoFm = new double[Sinos];
        private int _proximoSino;

        // percussao
        private float _bumboT = 9, _chimbalT = 9, _bumboAmp, _chimbalAmp;
        private double _bumboFase;
        private float _hpChimbal, _xChimbal;

        // ambiente
        private float _lpVento, _ventoMod, _grilosT = 9, _metalT = 9, _metalFreq;
        private double _grilosFase, _metalFc, _metalFm;

        // reverb (Schroeder: 4 pentes em paralelo, 2 passa-tudo em serie)
        private readonly float[][] _pente = new float[4][];
        private readonly int[] _penteIdx = new int[4];
        private readonly float[] _penteLp = new float[4];
        private readonly float[][] _passaTudo = new float[2][];
        private readonly int[] _ptIdx = new int[2];

        private void Awake()
        {
            _taxa = AudioSettings.outputSampleRate;
            for (int i = 0; i < Cordas; i++) _corda[i] = new float[MaxCorda];
            int[] pentes = { 1116, 1188, 1277, 1356 };
            for (int i = 0; i < 4; i++) _pente[i] = new float[pentes[i] * _taxa / 44100 + 1];
            _passaTudo[0] = new float[556 * _taxa / 44100 + 1];
            _passaTudo[1] = new float[441 * _taxa / 44100 + 1];
            _tema = new TemaMusical { Drone = 0, Pad = 0, Dedilhado = 0, Sino = 0, Percussao = 0, Vento = 0 };
            AudioSource fonte = GetComponent<AudioSource>();
            // Um clipe mudo em laco so para o Unity chamar OnAudioFilterRead.
            fonte.clip = AudioClip.Create("silencio", _taxa, 1, _taxa, false);
            fonte.loop = true;
            fonte.spatialBlend = 0;
            fonte.playOnAwake = false;
            fonte.Play();
        }

        public void Tocar(TemaMusical tema)
        {
            if (tema == null || tema == _tema || tema == _pedido) return;
            _pedido = tema;
        }

        // ---------------------------------------------------------------- utilidades

        private float Aleatorio()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xffffff) / 16777216f;
        }

        private float Branco() => Aleatorio() * 2 - 1;

        private static float Midi(int nota) => 440f * Mathf.Pow(2f, (nota - 69) / 12f);

        private int Nota(TemaMusical t, int grau, int oitava)
        {
            int n = t.Escala.Length;
            int o = Mathf.FloorToInt(grau / (float)n);
            int g = ((grau % n) + n) % n;
            return t.Tonica + t.Escala[g] + 12 * (o + oitava);
        }

        // ---------------------------------------------------------------- composicao

        private void Passo()
        {
            TemaMusical t = _tema;
            int noCompasso = _passo % 16;
            float tensao = Mathf.Lerp(0, 1, _intensidade);

            if (noCompasso == 0 && (_passo / 16) % 2 == 0 && t.Pad > 0.01f)
            {
                _acorde = (_acorde + 1) % t.Progressao.Length;
                int raiz = t.Progressao[_acorde];
                float dur = 16 * 2 * _amostrasPorPasso / (float)_taxa;
                DispararPad(Midi(Nota(t, raiz, 0)), dur, t.Pad);
                DispararPad(Midi(Nota(t, raiz + 2, 0)), dur, t.Pad * 0.8f);
                DispararPad(Midi(Nota(t, raiz + 4, 1)), dur, t.Pad * 0.6f);
            }

            // Melodia em caminhada: passo pequeno na escala, as vezes um salto.
            float chance = t.Dedilhado * (noCompasso % 4 == 0 ? 0.55f : noCompasso % 2 == 0 ? 0.25f : 0.08f) * (1 + tensao * 0.6f);
            if (Aleatorio() < chance)
            {
                int salto = Aleatorio() < 0.2f ? (Aleatorio() < 0.5f ? -3 : 4) : (Aleatorio() < 0.5f ? -1 : 1);
                _ultimaNota = Mathf.Clamp(_ultimaNota + salto, -2, 9);
                DispararCorda(Midi(Nota(t, _ultimaNota, 1)), 0.18f + 0.1f * Aleatorio());
                if (Aleatorio() < 0.15f) DispararCorda(Midi(Nota(t, _ultimaNota + 2, 1)), 0.1f);
            }

            if (Aleatorio() < t.Sino * 0.035f * (1 - _noite * 0.3f))
                DispararSino(Midi(Nota(t, (int)(Aleatorio() * 7), 2)), 0.12f);

            float perc = t.Percussao * Mathf.Clamp01((_intensidade - 0.15f) * 1.5f);
            if (perc > 0.01f)
            {
                bool bumbo = noCompasso == 0 || noCompasso == 8 || (_intensidade > 0.6f && (noCompasso == 6 || noCompasso == 11));
                if (bumbo)
                {
                    _bumboT = 0;
                    _bumboFase = 0;
                    _bumboAmp = 0.5f * perc;
                }
                if (noCompasso % 2 == 0 || (_intensidade > 0.7f && Aleatorio() < 0.4f))
                {
                    _chimbalT = 0;
                    _chimbalAmp = (noCompasso % 4 == 2 ? 0.16f : 0.08f) * perc;
                }
            }
            else if (_noite > 0.5f && t.Percussao > 0 && (noCompasso == 0 || noCompasso == 3))
            {
                // De noite, sem combate: um batimento abafado, o coracao de quem esta fora.
                _bumboT = 0;
                _bumboFase = 0;
                _bumboAmp = 0.18f * _noite;
            }

            if (t.Grilos > 0 && _noite > 0.3f && noCompasso % 4 == 0 && Aleatorio() < 0.5f) _grilosT = 0;
            if (t.Metal > 0 && Aleatorio() < t.Metal * 0.012f)
            {
                _metalT = 0;
                _metalFreq = 60 + Aleatorio() * 90;
                _metalFc = 0;
                _metalFm = 0;
            }
        }

        private void DispararPad(float freq, float dur, float amp)
        {
            int melhor = 0;
            float maisVelho = -1;
            for (int i = 0; i < Pads; i++)
            {
                float idade = _padT[i] - _padDur[i];
                if (idade > maisVelho)
                {
                    maisVelho = idade;
                    melhor = i;
                }
            }
            _padFreq[melhor] = freq;
            _padT[melhor] = 0;
            _padDur[melhor] = dur;
            _padAmp[melhor] = amp;
        }

        private void DispararCorda(float freq, float amp)
        {
            int i = _proximaCorda;
            _proximaCorda = (_proximaCorda + 1) % Cordas;
            int len = Mathf.Clamp(Mathf.RoundToInt(_taxa / freq), 8, MaxCorda);
            float[] b = _corda[i];
            float lp = 0;
            for (int k = 0; k < len; k++)
            {
                // Ruido ja filtrado: corda de nylon, nao de aco.
                lp += (Branco() - lp) * 0.5f;
                b[k] = lp;
            }
            _cordaLen[i] = len;
            _cordaPos[i] = 0;
            _cordaAmp[i] = amp;
            _cordaViva[i] = true;
        }

        private void DispararSino(float freq, float amp)
        {
            int i = _proximoSino;
            _proximoSino = (_proximoSino + 1) % Sinos;
            _sinoFreq[i] = freq;
            _sinoT[i] = 0;
            _sinoAmp[i] = amp;
            _sinoFc[i] = 0;
            _sinoFm[i] = 0;
        }

        // ---------------------------------------------------------------- audio

        private void OnAudioFilterRead(float[] dados, int canais)
        {
            TemaMusical pedido = _pedido;
            if (pedido != null && pedido != _tema)
            {
                _anterior = _tema;
                _tema = pedido;
                _pedido = null;
                _mistura = 0;
            }
            TemaMusical t = _tema;
            float dt = 1f / _taxa;
            _amostrasPorPasso = Mathf.Max(1, (int)(_taxa * 60f / Mathf.Max(30, t.Bpm) / 4f));
            float volume = Volume, volAmb = VolumeAmbiente;
            int quadros = dados.Length / canais;

            for (int q = 0; q < quadros; q++)
            {
                _intensidade += (IntensidadeAlvo - _intensidade) * 0.00002f;
                _noite += (NoiteAlvo - _noite) * 0.00001f;
                if (_mistura < 1) _mistura = Mathf.Min(1, _mistura + dt / 4f);
                if (++_contaPasso >= _amostrasPorPasso)
                {
                    _contaPasso = 0;
                    Passo();
                    _passo++;
                }

                float misturaDrone = Mathf.Lerp(_anterior?.Drone ?? 0, t.Drone, _mistura);
                float brilho = Mathf.Lerp(_anterior?.Brilho ?? 1, t.Brilho, _mistura) * (1 - _noite * 0.35f) * (1 + _intensidade * 0.6f);
                float vento = Mathf.Lerp(_anterior?.Vento ?? 0, t.Vento, _mistura);
                int tonica = t.Tonica;

                // Drone: duas serras desafinadas e um seno grave, filtro que respira.
                _lfo += 0.06 * dt;
                float fDrone = Midi(tonica - 12);
                _fd1 += fDrone * 1.003 * dt;
                _fd2 += fDrone * 0.997 * dt;
                _fd3 += fDrone * 0.5 * dt;
                float s1 = (float)(2 * (_fd1 - Math.Floor(_fd1 + 0.5)));
                float s2 = (float)(2 * (_fd2 - Math.Floor(_fd2 + 0.5)));
                float sub = (float)Math.Sin(_fd3 * 2 * Math.PI);
                float corte = (180 + 380 * (0.5f + 0.5f * (float)Math.Sin(_lfo * 2 * Math.PI))) * brilho;
                float k = Mathf.Clamp01(2 * Mathf.PI * corte * dt);
                _lpDrone += ((s1 + s2) * 0.5f - _lpDrone) * k;
                float musica = (_lpDrone * 0.5f + sub * 0.35f) * misturaDrone * 0.35f;

                for (int i = 0; i < Pads; i++)
                {
                    if (_padAmp[i] <= 0) continue;
                    _padT[i] += dt;
                    float env = Sintese.Adsr(_padT[i], _padDur[i], 1.8f, 0.5f, 0.75f, 2.5f);
                    if (_padT[i] > _padDur[i] + 10)
                    {
                        _padAmp[i] = 0;
                        continue;
                    }
                    float f = _padFreq[i];
                    _padF1[i] += f * dt;
                    _padF2[i] += f * 1.004 * dt;
                    _padF3[i] += f * 2.001 * dt;
                    float v = Tri(_padF1[i]) + Tri(_padF2[i]) * 0.8f + Tri(_padF3[i]) * 0.25f * brilho;
                    musica += v * env * _padAmp[i] * 0.06f;
                }

                for (int i = 0; i < Cordas; i++)
                {
                    if (!_cordaViva[i]) continue;
                    float[] b = _corda[i];
                    int len = _cordaLen[i], p = _cordaPos[i];
                    int prox = p + 1 >= len ? 0 : p + 1;
                    float saida = b[p];
                    b[p] = (b[p] + b[prox]) * 0.4985f;
                    _cordaPos[i] = prox;
                    musica += saida * _cordaAmp[i];
                    if (p == 0 && Mathf.Abs(saida) < 1e-4f) _cordaViva[i] = false;
                }

                for (int i = 0; i < Sinos; i++)
                {
                    if (_sinoAmp[i] <= 0) continue;
                    _sinoT[i] += dt;
                    float tt = _sinoT[i];
                    _sinoFc[i] += _sinoFreq[i] * dt;
                    _sinoFm[i] += _sinoFreq[i] * 3.5 * dt;
                    float indice = 3f * Mathf.Exp(-tt / 0.4f);
                    float v = (float)Math.Sin(2 * Math.PI * _sinoFc[i] + indice * Math.Sin(2 * Math.PI * _sinoFm[i]));
                    musica += v * Mathf.Exp(-tt / 1.6f) * _sinoAmp[i];
                    if (tt > 6) _sinoAmp[i] = 0;
                }

                if (_bumboT < 1)
                {
                    _bumboT += dt;
                    float f = 45 + 80 * Mathf.Exp(-_bumboT / 0.03f);
                    _bumboFase += f * dt;
                    musica += (float)Math.Sin(_bumboFase * 2 * Math.PI) * Mathf.Exp(-_bumboT / 0.18f) * _bumboAmp;
                }
                if (_chimbalT < 0.5f)
                {
                    _chimbalT += dt;
                    float x = Branco();
                    _hpChimbal = 0.85f * (_hpChimbal + x - _xChimbal);
                    _xChimbal = x;
                    musica += _hpChimbal * Mathf.Exp(-_chimbalT / 0.025f) * _chimbalAmp;
                }

                // Ambiente.
                float ambiente = 0;
                if (vento > 0)
                {
                    _ventoMod += (Aleatorio() - 0.5f) * 0.0004f;
                    _ventoMod = Mathf.Clamp(_ventoMod, -1, 1);
                    float cv = Mathf.Clamp01(2 * Mathf.PI * (380 + 260 * _ventoMod) * dt);
                    _lpVento += (Branco() - _lpVento) * cv;
                    ambiente += _lpVento * vento * (0.6f + 0.4f * _ventoMod) * 0.8f;
                }
                if (_grilosT < 0.25f)
                {
                    _grilosT += dt;
                    _grilosFase += 4300 * dt;
                    float pulso = Mathf.Sin(_grilosT * 2 * Mathf.PI * 32) > 0 ? 1 : 0;
                    ambiente += (float)Math.Sin(_grilosFase * 2 * Math.PI) * pulso * 0.025f * t.Grilos * _noite;
                }
                if (_metalT < 3)
                {
                    _metalT += dt;
                    _metalFc += _metalFreq * dt;
                    _metalFm += _metalFreq * 2.76 * dt;
                    float v = (float)Math.Sin(2 * Math.PI * _metalFc + 4 * Mathf.Exp(-_metalT / 0.3f) * Math.Sin(2 * Math.PI * _metalFm));
                    ambiente += v * Mathf.Exp(-_metalT / 0.8f) * 0.12f * t.Metal;
                }

                float seco = musica * volume + ambiente * volAmb;
                float rev = Reverb(seco * 0.35f + ambiente * volAmb * 0.4f);
                float l = Sintese.Distorcer(seco + rev, 0.9f);
                for (int c = 0; c < canais; c++) dados[q * canais + c] = l;
                _amostra++;
            }
        }

        private static float Tri(double fase)
        {
            float f = (float)(fase - Math.Floor(fase));
            return 1 - 4 * Mathf.Abs(f - 0.5f);
        }

        private float Reverb(float entrada)
        {
            float soma = 0;
            for (int i = 0; i < 4; i++)
            {
                float[] b = _pente[i];
                int idx = _penteIdx[i];
                float saida = b[idx];
                _penteLp[i] = saida * 0.75f + _penteLp[i] * 0.25f;
                b[idx] = entrada + _penteLp[i] * 0.83f;
                _penteIdx[i] = idx + 1 >= b.Length ? 0 : idx + 1;
                soma += saida;
            }
            soma *= 0.25f;
            for (int i = 0; i < 2; i++)
            {
                float[] b = _passaTudo[i];
                int idx = _ptIdx[i];
                float atrasado = b[idx];
                float saida = -soma + atrasado;
                b[idx] = soma + atrasado * 0.5f;
                _ptIdx[i] = idx + 1 >= b.Length ? 0 : idx + 1;
                soma = saida;
            }
            return soma;
        }
    }
}
