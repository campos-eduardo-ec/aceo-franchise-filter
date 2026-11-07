// Filtro de Franquias 0.2: filtro na lista de ofertas de franquia + prateleiras mistas.
//
// Lista de ofertas (BusinessRoomUI.cs, descompilado):
//   ToggleContractPanel(room) e UpdatePanel() (a cada 1 s real, GenericObjectPanelUI.cs:119-125)
//   -> ClearFranchiseContractContainer() -> GenerateFranchiseContractContainers(room)
//   -> AddToContractRoomList(franchise, room) cria um ContractRoomContainerUI por oferta.
//   O plugin grava classe/validade de cada cartao (InfoFranquia) e reaplica filtro e ordem
//   depois de TODA geracao da lista. A barra fica ancorada no topo do FranchiseContractPanel
//   (285 de largura) e o ViewportDisplay encolhe para dar lugar a ela.
//
// Prateleiras (PlaceableRoom.cs:735-812): o jogo sorteia UM produto por prateleira e repete o
// mesmo sprite em todos os pontos. Com "Misturar produtos", cada prateleira vira blocos de
// produtos diferentes (como uma gondola real), com cor sorteada por ponto quando o produto
// tem varias cores. Vendas nao mudam: o passageiro compra por FranchiseModel.GetRandomProduct.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FiltroFranquias
{
    [BepInPlugin(Guid, "ACEO Franchise Filter", "0.3.2")] // nome publico desde 29/10 (serie Aeroportos BR); o Guid fica, para nao perder a config
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "br.eduardocampos.aceo.filtrofranquias";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> CfgSoCabem;
        internal static ConfigEntry<bool> CfgMisturar;
        internal static ConfigEntry<bool> CfgAlinhar;
        internal static ConfigEntry<bool> CfgOcultarOriginais;

        private void Awake()
        {
            Log = Logger;
            CfgSoCabem = Config.Bind("Filtro", "Mostrar so as que cabem", false,
                "Lembra a ultima escolha do botao 'Cabem' na lista de ofertas.");
            CfgMisturar = Config.Bind("Prateleiras", "Misturar produtos", true,
                "Cada prateleira mostra blocos de produtos diferentes da franquia, em vez de um so produto repetido.");
            CfgAlinhar = Config.Bind("Prateleiras", "Alinhar com a prateleira", true,
                "Gira os produtos junto com a prateleira, em vez de rotacao aleatoria.");
            CfgOcultarOriginais = Config.Bind("Franquias", "Ocultar franquias originais do jogo", false,
                "Se verdadeiro, o mod pula as pastas foodfranchises/shopfranchises originais ao carregar o jogo, sem gravar nada no GameSetting.json. " +
                "Os interruptores 'Default food/shop businesses' do menu Mods continuam valendo por conta propria: se estiverem desligados, " +
                "as originais ficam ocultas mesmo com esta opcao em false.");
            Filtro.SoCabem = CfgSoCabem.Value;
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("ACEO Franchise Filter (Filtro de Franquias) carregado.");
        }
    }

    /// <summary>Dados de cada cartao da lista, gravados quando o jogo cria o cartao.</summary>
    internal class InfoFranquia : MonoBehaviour
    {
        public int Estrelas;
        public bool Cabe;
        public string Nome;
    }

    internal static class Filtro
    {
        internal const string NomeBarra = "FiltroFranquiasBarra";
        internal static readonly bool[] Estrelas = { true, true, true, true, true };
        internal static bool SoCabem;

        /// <summary>Rotulo da barra no idioma do jogo (LocalizationManager.Culture): portugues em pt, ingles nos outros.</summary>
        internal static string Texto(string pt, string en)
        {
            try { return LocalizationManager.Culture?.TwoLetterISOLanguageName == "pt" ? pt : en; }
            catch { return en; }
        }

        internal static readonly AccessTools.FieldRef<BusinessRoomUI, List<ContractRoomContainerUI>> Lista =
            AccessTools.FieldRefAccess<BusinessRoomUI, List<ContractRoomContainerUI>>("currentFranchiseContracts");
        internal static readonly AccessTools.FieldRef<BusinessRoomUI, Transform> Painel =
            AccessTools.FieldRefAccess<BusinessRoomUI, Transform>("franchiseContractPanel");
        internal static readonly AccessTools.FieldRef<BusinessRoomUI, TextMeshProUGUI> Cabecalho =
            AccessTools.FieldRefAccess<BusinessRoomUI, TextMeshProUGUI>("headerText");
        internal static readonly MethodInfo PodeAssinar =
            AccessTools.PropertyGetter(typeof(ContractRoomContainerUI), "CanSign");

        internal static bool Todas => Estrelas.All(e => e);

        internal static void CliqueTodas()
        {
            for (int i = 0; i < Estrelas.Length; i++) Estrelas[i] = true;
        }

        internal static void CliqueEstrela(int indice)
        {
            if (Todas)
            {
                // Com tudo ligado, clicar numa estrela mostra so ela.
                for (int i = 0; i < Estrelas.Length; i++) Estrelas[i] = i == indice;
                return;
            }
            Estrelas[indice] = !Estrelas[indice];
            if (!Estrelas.Any(e => e)) CliqueTodas();
        }

        internal static void CliqueCabem()
        {
            SoCabem = !SoCabem;
            if (Plugin.CfgSoCabem != null) Plugin.CfgSoCabem.Value = SoCabem;
        }

        /// <param name="voltarAoTopo">So ao abrir o painel ou clicar num filtro; nao na recriacao periodica.</param>
        internal static void Aplicar(BusinessRoomUI ui, bool voltarAoTopo)
        {
            if (ui == null) return;
            List<ContractRoomContainerUI> lista = Lista(ui);
            if (lista == null) return;

            List<InfoFranquia> itens = lista.Where(c => c != null)
                .Select(c => c.GetComponent<InfoFranquia>())
                .Where(i => i != null)
                .OrderByDescending(i => i.Cabe)
                .ThenByDescending(i => i.Estrelas)
                .ThenBy(i => i.Nome)
                .ToList();

            int visiveis = 0;
            for (int i = 0; i < itens.Count; i++)
            {
                InfoFranquia info = itens[i];
                info.transform.SetSiblingIndex(i);
                bool mostra = Estrelas[Mathf.Clamp(info.Estrelas, 1, 5) - 1] && (!SoCabem || info.Cabe);
                if (info.gameObject.activeSelf != mostra) info.gameObject.SetActive(mostra);
                if (mostra) visiveis++;
            }

            Transform painel = Painel(ui);
            if (painel == null) return;
            BarraFiltro barra = BarraFiltro.Garantir(ui, painel);
            if (barra != null) barra.Atualizar(visiveis, itens.Count);
            if (voltarAoTopo)
            {
                ScrollRect rolagem = painel.GetComponentInChildren<ScrollRect>(true);
                if (rolagem != null) rolagem.verticalNormalizedPosition = 1f;
            }
        }
    }

    /// <summary>Barra uGUI ancorada no topo do painel de ofertas.</summary>
    internal class BarraFiltro : MonoBehaviour
    {
        private BusinessRoomUI ui;
        private readonly List<Botao> botoes = new List<Botao>();
        private TextMeshProUGUI contagem;

        private class Botao
        {
            public Image Fundo;
            public System.Func<bool> Ativo;
        }

        private static readonly Color CorLigado = new Color(0.30f, 0.62f, 0.36f, 1f);
        private static readonly Color CorDesligado = new Color(0.32f, 0.32f, 0.38f, 1f);
        private const float Altura = 30f;

        private static Transform painelSemBarra; // painel onde a criacao falhou: nao tenta nem registra de novo

        internal static BarraFiltro Garantir(BusinessRoomUI ui, Transform painel)
        {
            Transform existente = painel.Find(Filtro.NomeBarra);
            if (existente != null) return existente.GetComponent<BarraFiltro>();
            if (painelSemBarra == painel) return null;
            try
            {
                BarraFiltro barra = Criar(ui, painel);
                if (barra == null) painelSemBarra = painel;
                return barra;
            }
            catch (System.Exception e)
            {
                Transform parcial = painel.Find(Filtro.NomeBarra + "_montando");
                if (parcial != null) Object.Destroy(parcial.gameObject);
                painelSemBarra = painel;
                Plugin.Log.LogError($"Nao consegui criar a barra de filtro (nao tento de novo neste painel): {e}");
                return null;
            }
        }

        private static BarraFiltro Criar(BusinessRoomUI ui, Transform painel)
        {
            RectTransform area = painel.Find("ViewportDisplay") as RectTransform;
            if (area == null)
            {
                Plugin.Log.LogWarning("ViewportDisplay nao encontrado; barra de filtro nao criada.");
                return null;
            }
            TMP_FontAsset fonte = Filtro.Cabecalho(ui)?.font ?? TMP_Settings.defaultFontAsset;

            var go = new GameObject(Filtro.NomeBarra + "_montando", typeof(RectTransform));
            var barra = go.AddComponent<BarraFiltro>();
            barra.ui = ui;
            var rt = (RectTransform)go.transform;
            rt.SetParent(painel, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, Altura);
            rt.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(4, 4, 3, 3);
            h.spacing = 3f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            // Nenhuma fonte TMP do jogo tem o caractere estrela: asterisco dourado (cor das estrelas do jogo).
            barra.AdicionarBotao(fonte, Filtro.Texto("Todas", "All"), 38f, 52f, () => Filtro.Todas, Filtro.CliqueTodas);
            for (int i = 0; i < 5; i++)
            {
                int indice = i;
                barra.AdicionarBotao(fonte, $"{i + 1}<size=150%><voffset=-4><color=#FFCD00>*</color></voffset></size>", 24f, 32f,
                    () => !Filtro.Todas && Filtro.Estrelas[indice], () => Filtro.CliqueEstrela(indice));
            }
            barra.AdicionarBotao(fonte, Filtro.Texto("Cabem", "Fits"), 42f, 56f, () => Filtro.SoCabem, Filtro.CliqueCabem);

            var textoGo = new GameObject("Contagem", typeof(RectTransform));
            textoGo.transform.SetParent(go.transform, false);
            barra.contagem = textoGo.AddComponent<TextMeshProUGUI>();
            if (fonte != null) barra.contagem.font = fonte;
            barra.contagem.fontSize = 13f;
            barra.contagem.enableWordWrapping = false;
            barra.contagem.overflowMode = TextOverflowModes.Ellipsis;
            barra.contagem.alignment = TextAlignmentOptions.MidlineRight;
            barra.contagem.color = Color.white;
            barra.contagem.raycastTarget = false;
            var le = textoGo.AddComponent<LayoutElement>();
            le.minWidth = 40f;
            le.flexibleWidth = 1f;

            // Acima da lista e abaixo do HoveredContractUI (tooltip).
            rt.SetSiblingIndex(area.GetSiblingIndex() + 1);
            // Por ultimo: a lista desce para dar lugar a barra. O jogo so procura ViewportDisplay pelo nome
            // (BusinessRoomUI.cs:52) e nunca mexe no rect dele.
            area.offsetMax = new Vector2(area.offsetMax.x, area.offsetMax.y - Altura);
            go.name = Filtro.NomeBarra; // so pode ser encontrada pelo Find depois de pronta
            return barra;
        }

        private void AdicionarBotao(TMP_FontAsset fonte, string rotulo, float minimo, float preferido,
            System.Func<bool> ativo, System.Action acao)
        {
            var go = new GameObject("Botao " + rotulo, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var fundo = go.AddComponent<Image>();
            fundo.color = CorDesligado;

            // Mesmo padrao dos botoes do jogo: som ao passar e ao clicar (AudioEmitter/AudioEmission).
            var som = go.AddComponent<AudioEmitter>();
            som.audioEmissions = new[]
            {
                new AudioEmission { audioClipToEmit = Enums.AudioClip.PointerEnter, pointerInvokerType = Enums.PointerInvokerType.Enter, randomizePitch = true },
                new AudioEmission { audioClipToEmit = Enums.AudioClip.PointerClick, pointerInvokerType = Enums.PointerInvokerType.Click },
            };

            var botao = go.AddComponent<Button>();
            botao.targetGraphic = fundo;
            botao.navigation = new Navigation { mode = Navigation.Mode.None }; // nunca fica selecionado; Return nao reaciona
            botao.onClick.AddListener(() =>
            {
                acao();
                Filtro.Aplicar(ui, voltarAoTopo: true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = minimo;
            le.preferredWidth = preferido;

            var textoGo = new GameObject("Texto", typeof(RectTransform));
            textoGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textoGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var texto = textoGo.AddComponent<TextMeshProUGUI>();
            if (fonte != null) texto.font = fonte;
            texto.richText = true;
            texto.text = rotulo;
            texto.fontSize = 13f;
            texto.enableWordWrapping = false;
            texto.alignment = TextAlignmentOptions.Center;
            texto.color = Color.white;
            texto.raycastTarget = false;

            botoes.Add(new Botao { Fundo = fundo, Ativo = ativo });
        }

        internal void Atualizar(int visiveis, int total)
        {
            foreach (Botao b in botoes)
            {
                Color cor = b.Ativo() ? CorLigado : CorDesligado;
                if (b.Fundo.color != cor) b.Fundo.color = cor;
            }
            if (contagem != null)
            {
                string texto = total == 0 ? "" : $"{visiveis} {Filtro.Texto("de", "of")} {total}";
                if (contagem.text != texto) contagem.text = texto;
            }
        }
    }

    [HarmonyPatch(typeof(BusinessRoomUI), nameof(BusinessRoomUI.AddToContractRoomList))]
    internal static class Patch_AddToContractRoomList
    {
        private static void Postfix(BusinessRoomUI __instance, FranchiseModel franchise)
        {
            try
            {
                List<ContractRoomContainerUI> lista = Filtro.Lista(__instance);
                if (lista == null || lista.Count == 0 || franchise == null) return;
                ContractRoomContainerUI cartao = lista[lista.Count - 1];
                InfoFranquia info = cartao.gameObject.GetComponent<InfoFranquia>() ?? cartao.gameObject.AddComponent<InfoFranquia>();
                info.Estrelas = (int)franchise.businessClass + 1;
                info.Nome = franchise.businessName ?? "";
                info.Cabe = Filtro.PodeAssinar != null && (bool)Filtro.PodeAssinar.Invoke(cartao, null);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Falha ao registrar cartao de franquia: {e}");
            }
        }
    }

    // Cobre a abertura do painel e a recriacao periodica (UpdatePanel a cada 1 s).
    [HarmonyPatch(typeof(BusinessRoomUI), "GenerateFranchiseContractContainers")]
    internal static class Patch_GenerateFranchiseContractContainers
    {
        private static void Postfix(BusinessRoomUI __instance)
        {
            try
            {
                Filtro.Aplicar(__instance, voltarAoTopo: false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Falha ao aplicar o filtro: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(BusinessRoomUI), nameof(BusinessRoomUI.ToggleContractPanel))]
    internal static class Patch_ToggleContractPanel
    {
        private static void Postfix(BusinessRoomUI __instance)
        {
            if (!__instance.contractPanelOpen) return;
            try
            {
                Filtro.Aplicar(__instance, voltarAoTopo: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Falha ao aplicar o filtro: {e}");
            }
        }
    }

    // Cache de imagem de produto customizado: uma textura por arquivo para todas as prateleiras.
    // O jogo relia o PNG do disco a cada prateleira (ProductListManager.cs:62-78), sem cache.
    [HarmonyPatch(typeof(ProductListManager), nameof(ProductListManager.GetSpritesColorablesFromProduct))]
    internal static class Patch_GetSpritesColorablesFromProduct
    {
        internal static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static bool Prefix(Product product, ref SpriteColorables __result)
        {
            if (product == null || !product.isCustom) return true; // vanilla vem do atlas, sem disco
            string chave = product.imagePath ?? "";
            bool temCache = Cache.TryGetValue(chave, out Sprite sprite);
            // Recarrega so se nunca carregou ou se o Unity destruiu a textura (troca de cena).
            if (!temCache || (!ReferenceEquals(sprite, null) && sprite == null))
            {
                sprite = Utils.LoadImage(product.imagePath, 350, useMipMap: true, encodeStringToByteArray: false, compress: true);
                Cache[chave] = sprite; // null de verdade = arquivo inexistente; nao tenta de novo
            }
            // Cores sao do produto, nao da imagem: nunca vem do cache.
            __result = new SpriteColorables { sprite = sprite, avialableColors = product.availableColors };
            return false;
        }
    }

    // Volta ao menu (BusinessController.cs:437, antes do LoadScene): esquece as texturas antigas.
    [HarmonyPatch(typeof(ProductListManager), nameof(ProductListManager.ClearProductList))]
    internal static class Patch_ClearProductList
    {
        private static void Postfix() => Patch_GetSpritesColorablesFromProduct.Cache.Clear();
    }

    [HarmonyPatch(typeof(PlaceableRoom), nameof(PlaceableRoom.PopulateSingleRoomShelf))]
    internal static class Patch_PopulateSingleRoomShelf
    {
        private static readonly MethodInfo SetProductSprite = AccessTools.Method(typeof(PlaceableRoom), "SetProductSprite");
        private static readonly MethodInfo GetProductColor = AccessTools.Method(typeof(PlaceableRoom), "GetProductColor");
        private static readonly MethodInfo GetProductRotation = AccessTools.Method(typeof(PlaceableRoom), "GetProductRotation");
        // Vizinhos reais distam 0,4 a 0,78 m; saltos entre segmentos da prateleira, 0,99 m ou mais (ShelfModel.cs:103-372).
        private const float Vizinho = 0.85f;

        private struct Variante { public Product P; public SpriteColorables Sc; }

        private static bool Prefix(PlaceableRoom __instance, ShelfModel shelf, Product product, bool enableEffect)
        {
            bool misturar = Plugin.CfgMisturar != null && Plugin.CfgMisturar.Value;
            bool alinhar = Plugin.CfgAlinhar != null && Plugin.CfgAlinhar.Value;
            if (!misturar && !alinhar) return true;
            if (SetProductSprite == null || GetProductColor == null || GetProductRotation == null) return true;
            try
            {
                if (shelf == null || shelf.interactionPoints == null || product == null) return true;
                if (!(__instance.CurrentBusiness is FranchiseModel franquia)) return true;

                List<List<Variante>> tipos = Tipos(franquia);
                if (tipos.Count == 0) return true; // nada com imagem: o original tambem nao desenha
                if (!misturar)
                {
                    // So "Alinhar": um produto na prateleira toda, como o jogo; troca o sorteado so se ele nao tiver imagem.
                    SpriteColorables scJogo = ProductListManager.GetSpritesColorablesFromProduct(product);
                    Variante unico = scJogo.sprite != null
                        ? new Variante { P = product, Sc = scJogo }
                        : tipos[Random.Range(0, tipos.Count)][0];
                    tipos = new List<List<Variante>> { new List<Variante> { unico } };
                }

                int pontos = shelf.interactionPoints.Length;
                Vector3 giroPrateleira = GiroAlinhado(shelf);
                int anterior = -1;
                int i = 0;
                while (i < pontos)
                {
                    // Prateleira pequena (3 ou 4 pontos): um produto por ponto. Maiores: blocos de 2 a 4.
                    int bloco = pontos <= 4 ? 1 : Mathf.Min(Random.Range(2, 5), pontos - i);
                    int escolha = Random.Range(0, tipos.Count);
                    if (tipos.Count > 1 && escolha == anterior)
                        escolha = (escolha + 1 + Random.Range(0, tipos.Count - 1)) % tipos.Count;
                    anterior = escolha;
                    List<Variante> vs = tipos[escolha];
                    Variante v = vs[Random.Range(0, vs.Count)];
                    bool corPorPonto = v.P.canHaveAnyColor || (v.P.availableColors != null && v.P.availableColors.Length > 1);
                    Color cor = (Color)GetProductColor.Invoke(__instance, new object[] { v.P, v.Sc });
                    Vector3 giro = alinhar ? giroPrateleira : (Vector3)GetProductRotation.Invoke(__instance, new object[] { v.P });
                    for (int k = 0; k < bloco; k++, i++)
                    {
                        if (k > 0 && Distante(shelf, i - 1, i)) break; // bloco nao atravessa para outro lado da prateleira
                        if (corPorPonto && k > 0) cor = (Color)GetProductColor.Invoke(__instance, new object[] { v.P, v.Sc });
                        SetProductSprite.Invoke(__instance, new object[] { shelf.interactionPoints[i], giro, cor, v.Sc });
                    }
                }
                if (enableEffect)
                    Singleton<ConstructionController>.Instance.TriggerConstructionEffect(shelf.Position, __instance.Floor, isInside: true, Enums.ConstructionOperation.Construct);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Falha ao montar prateleira, usando o padrao do jogo: {e}");
                return true;
            }
        }

        // Peso igual por tipo; dentro do tipo, uma variante por sprite distinto (qualidade com arte propria).
        private static List<List<Variante>> Tipos(FranchiseModel franquia)
        {
            var tipos = new List<List<Variante>>();
            foreach (IGrouping<string, Product> g in ProductListManager
                         .GetListOfPossibleProducts(franquia.products, franquia.businessClass)
                         .Where(p => p != null).GroupBy(p => p.productType))
            {
                var variantes = new List<Variante>();
                var vistos = new HashSet<Sprite>();
                foreach (Product p in g)
                {
                    SpriteColorables sc = ProductListManager.GetSpritesColorablesFromProduct(p);
                    if (sc.sprite != null && vistos.Add(sc.sprite)) variantes.Add(new Variante { P = p, Sc = sc });
                }
                if (variantes.Count > 0) tipos.Add(variantes);
            }
            return tipos;
        }

        // So a Pequena media (v0 escalonada, v1 de parede) tem frente. Nas simetricas, 180 e 270 viram 0 e 90:
        // o produto segue o eixo da prateleira sem ficar de ponta-cabeca.
        private static Vector3 GiroAlinhado(ShelfModel shelf)
        {
            float z = Mathf.Repeat(Mathf.Round(shelf.transform.eulerAngles.z), 360f);
            bool temFrente = shelf.objectSize == Enums.ThreeStepScale.Small && shelf.objectQuality == Enums.QualityType.Medium;
            if (!temFrente) z = Mathf.Repeat(z, 180f);
            return new Vector3(0f, 0f, z);
        }

        private static bool Distante(ShelfModel shelf, int a, int b)
        {
            Transform ta = shelf.interactionPoints[a].transform, tb = shelf.interactionPoints[b].transform;
            return ta != null && tb != null && (ta.localPosition - tb.localPosition).sqrMagnitude > Vizinho * Vizinho;
        }
    }

    // Ocultar as franquias originais filtrando na origem, sem tocar no GameSettingManager
    // (o patch no getter fazia o menu Mods gravar false no GameSetting.json ao abrir).
    [HarmonyPatch(typeof(BusinessLoader), nameof(BusinessLoader.QueueBusiness))]
    internal static class Patch_QueueBusiness_OcultarOriginais
    {
        // BusinessController.cs:412 chama com isDefault=true para DataFiles/Companies/<Default|Eastern|Helicopter|Vintage>;
        // mods chegam com isDefault=false (ModManager.cs:84) e nunca sao filtrados.
        private static void Prefix(ref string[] path, bool isDefault)
        {
            if (!isDefault || path == null) return;
            if (Plugin.CfgOcultarOriginais == null || !Plugin.CfgOcultarOriginais.Value) return;
            string[] filtrado = path.Where(p =>
            {
                string t = (p ?? "").Replace("\\", "/").TrimEnd('/').ToLowerInvariant();
                return !t.EndsWith("foodfranchises") && !t.EndsWith("shopfranchises");
            }).ToArray();
            if (filtrado.Length != path.Length)
                Plugin.Log.LogInfo($"Franquias originais ocultadas: {path.Length - filtrado.Length} pasta(s) ignorada(s).");
            path = filtrado;
        }
    }

    [HarmonyPatch(typeof(BusinessController), nameof(BusinessController.Initialize))]
    internal static class Patch_AvisoSemFranquias
    {
        private static void Postfix(BusinessController __instance)
        {
            if (Plugin.CfgOcultarOriginais == null || !Plugin.CfgOcultarOriginais.Value) return;
            DynamicArray<Business> lista = __instance.allAvailableBusinesses;
            if (lista == null) return;
            for (int i = 0; i < lista.Length; i++)
                if (lista.array[i] != null && lista.array[i].businessType == Enums.BusinessType.Franchise) return;
            Plugin.Log.LogWarning("'Ocultar franquias originais' esta ligado e nenhum mod de franquia foi carregado. " +
                "O aviso 'No franchise businesses found' vem desta opcao, nao do menu Mods. Ative o LojasAeroportosBR ou desligue a opcao.");
        }
    }

    // O jogo guarda os mods ja carregados (ModManager.loadedMods) e so limpa ao voltar ao menu por um caminho
    // (BusinessController.ResetForMainMenu). Numa segunda carga na mesma sessao o mod nativo era pulado e as
    // franquias sumiam ("No franchise businesses found"). Limpar antes de cada inicializacao resolve.
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.InitializeMods))]
    internal static class Patch_RecarregarMods
    {
        private static void Prefix() => ModManager.ResetLoadedMods();
    }

    // A lista de ofertas guarda contractPanelOpen=true quando o painel da sala e fechado com ela aberta
    // (BusinessRoomUI.HidePanel nao reseta). O proximo clique so "fechava" o que ja estava invisivel.
    [HarmonyPatch(typeof(BusinessRoomUI), nameof(BusinessRoomUI.ToggleContractPanel))]
    internal static class Patch_ToggleContractPanel_Estado
    {
        private static void Prefix(BusinessRoomUI __instance)
        {
            Transform painel = Filtro.Painel(__instance);
            if (__instance.contractPanelOpen && painel != null && !painel.gameObject.activeInHierarchy)
                __instance.contractPanelOpen = false;
        }
    }
}
