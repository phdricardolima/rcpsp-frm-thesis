from pathlib import Path
import argparse
import zipfile

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.font_manager as font_manager
from scipy.stats import gaussian_kde


# ============================================================
# Configuração visual padrão da tese
# ============================================================

FONT_FAMILY = "NewsGotT"
FONT_SIZE = 12
MEDIAN_DECIMALS = 7
Y_TICK_ROTATION = 45


def configure_thesis_style():
    """Configura tipografia e tamanhos de texto para os gráficos.

    O nome NewsGotT é solicitado para compatibilidade com o padrão visual da tese.
    Se a fonte não estiver instalada no ambiente de execução, o Matplotlib usará
    automaticamente uma fonte substituta. Nenhum arquivo de fonte é embutido ou
    distribuído por este script.
    """
    available_fonts = {font.name for font in font_manager.fontManager.ttflist}

    if FONT_FAMILY in available_fonts:
        plt.rcParams["font.family"] = FONT_FAMILY
    else:
        # O ambiente atual pode não ter NewsGotT instalado. Nesse caso,
        # usa-se uma fonte substituta sem emitir avisos repetidos.
        # Em um computador com NewsGotT instalado, a fonte solicitada será aplicada.
        plt.rcParams["font.family"] = "DejaVu Sans"
        print(
            f"[AVISO] Fonte '{FONT_FAMILY}' não encontrada neste ambiente. "
            "Os gráficos serão gerados com fonte substituta. "
            "Instale a NewsGotT no sistema para aplicar a tipografia final da tese."
        )

    plt.rcParams["font.size"] = FONT_SIZE
    plt.rcParams["axes.titlesize"] = FONT_SIZE
    plt.rcParams["axes.labelsize"] = FONT_SIZE
    plt.rcParams["xtick.labelsize"] = FONT_SIZE
    plt.rcParams["ytick.labelsize"] = FONT_SIZE
    plt.rcParams["legend.fontsize"] = FONT_SIZE
    plt.rcParams["figure.titlesize"] = FONT_SIZE


configure_thesis_style()


# ============================================================
# Utilidades estatísticas
# ============================================================

def weighted_quantile(values, weights, q):
    values = np.asarray(values, dtype=float)
    weights = np.asarray(weights, dtype=float)

    mask = np.isfinite(values) & np.isfinite(weights) & (weights > 0)
    values = values[mask]
    weights = weights[mask]

    if len(values) == 0:
        return np.nan

    order = np.argsort(values)
    values = values[order]
    weights = weights[order]

    cumulative = np.cumsum(weights)
    cumulative = cumulative / cumulative[-1]

    return float(np.interp(q, cumulative, values))


def weighted_kde(values, weights, grid):
    values = np.asarray(values, dtype=float)
    weights = np.asarray(weights, dtype=float)

    mask = np.isfinite(values) & np.isfinite(weights) & (weights > 0)
    values = values[mask]
    weights = weights[mask]

    if len(values) < 2:
        return np.zeros_like(grid)

    if np.allclose(values, values[0]):
        return np.zeros_like(grid)

    weights = weights / weights.sum()

    kde = gaussian_kde(values, weights=weights, bw_method="scott")
    return kde(grid)


# ============================================================
# Função geral para ridgeline
# ============================================================

def plot_ridgeline(
    groups,
    values_col,
    weights_col,
    order,
    title,
    xlabel,
    output_stem,
    value_formatter,
    zero_line=True,
    note=None,
):
    all_values = []

    for group in order:
        if group in groups and not groups[group].empty:
            all_values.append(groups[group][values_col].to_numpy(dtype=float))

    if not all_values:
        raise ValueError("Nenhum dado disponível para gerar o gráfico.")

    all_values = np.concatenate(all_values)
    all_values = all_values[np.isfinite(all_values)]

    if len(all_values) == 0:
        raise ValueError("Todos os valores da variável estão ausentes ou inválidos.")

    xmin, xmax = np.nanpercentile(all_values, [0.5, 99.5])

    if np.isclose(xmin, xmax):
        xmin -= 1
        xmax += 1

    span = xmax - xmin
    xmin -= 0.08 * span
    xmax += 0.08 * span

    grid = np.linspace(xmin, xmax, 900)

    fig, ax = plt.subplots(figsize=(11.5, 7.5), dpi=180)

    spacing = 1.25
    ridge_height = 0.92
    y_positions = np.arange(len(order)) * spacing

    summary_rows = []

    for y, group in zip(y_positions, order):
        sub = groups[group]

        if sub.empty:
            continue

        values = sub[values_col].to_numpy(dtype=float)
        weights = sub[weights_col].to_numpy(dtype=float)

        density = weighted_kde(values, weights, grid)

        if np.max(density) > 0:
            density = density / np.max(density) * ridge_height

        line, = ax.plot(grid, y + density, linewidth=1.8)
        ax.fill_between(
            grid,
            y,
            y + density,
            alpha=0.35,
            color=line.get_color()
        )

        median = weighted_quantile(values, weights, 0.50)
        q1 = weighted_quantile(values, weights, 0.25)
        q3 = weighted_quantile(values, weights, 0.75)

        median_height = y + np.interp(median, grid, density)

        ax.vlines(
            median,
            y,
            median_height,
            linestyles="--",
            linewidth=1.4,
        )

        ax.annotate(
            value_formatter(median),
            xy=(median, median_height),
            xytext=(0, 7),
            textcoords="offset points",
            ha="left",
            va="bottom",
            fontsize=FONT_SIZE,
            bbox={
                "boxstyle": "round,pad=0.18",
                "facecolor": "white",
                "edgecolor": "0.7",
                "alpha": 0.92,
            },
        )

        summary_rows.append(
            {
                "grupo": group,
                "mediana": median,
                "q1": q1,
                "q3": q3,
                "n": len(sub),
            }
        )

    if zero_line and xmin < 0 < xmax:
        ax.axvline(0, linewidth=1.0, linestyle=":", alpha=0.8)
        ax.text(
            0,
            y_positions[-1] + ridge_height + 0.1,
            "0",
            ha="center",
            va="bottom",
            fontsize=FONT_SIZE,
        )

    ax.set_yticks(y_positions + ridge_height * 0.32)
    ax.set_yticklabels(order, fontsize=FONT_SIZE, rotation=Y_TICK_ROTATION, ha="right", va="center")

    ax.set_xlabel(xlabel, fontsize=FONT_SIZE)
    ax.set_ylabel("")
    ax.set_title(title, fontsize=FONT_SIZE, pad=14)

    ax.grid(axis="x", alpha=0.18)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

    ax.set_ylim(-0.05, y_positions[-1] + ridge_height + 0.28)
    ax.set_xlim(xmin, xmax)

    if note is None:
        note = (
            "Nota: densidades normalizadas. As linhas tracejadas indicam as medianas. "
            "A ponderação atribui peso total igual por instância."
        )

    fig.text(
        0.5,
        0.015,
        note,
        ha="center",
        va="bottom",
        fontsize=FONT_SIZE,
    )

    fig.tight_layout(rect=[0, 0.045, 1, 1])

    output_stem = Path(output_stem)
    output_stem.parent.mkdir(parents=True, exist_ok=True)

    png_path = output_stem.with_suffix(".png")
    pdf_path = output_stem.with_suffix(".pdf")
    csv_path = output_stem.with_name(output_stem.name + "_resumo.csv")

    fig.savefig(png_path, dpi=300, bbox_inches="tight")
    fig.savefig(pdf_path, bbox_inches="tight")
    plt.close(fig)

    pd.DataFrame(summary_rows).to_csv(csv_path, index=False)

    return png_path, pdf_path, csv_path


# ============================================================
# Pesos
# ============================================================

def add_baseline_weights(df):
    """
    Peso usado nas análises por baseline:
    cada instância recebe peso total igual.

    w_ij = 1 / (N * n_i)
    """
    n_instances = df["instance_id"].nunique()

    baseline_counts = (
        df[["instance_id", "baseline_id"]]
        .drop_duplicates()
        .groupby("instance_id")["baseline_id"]
        .nunique()
    )

    df = df.copy()
    df["n_i"] = df["instance_id"].map(baseline_counts)
    df["peso_baseline"] = 1.0 / (n_instances * df["n_i"])

    return df


def add_crashing_weights(df):
    """
    Peso usado nas análises por cenário de crashing:
    cada instância recebe peso total igual,
    cada baseline reparte o peso da instância,
    e cada cenário reparte o peso do baseline.

    w_ijs = 1 / (N * n_i * m_ij)
    """
    n_instances = df["instance_id"].nunique()

    baseline_counts = df.groupby("instance_id")["baseline_id"].nunique()
    scenario_counts = df.groupby(["instance_id", "baseline_id"]).size()

    df = df.copy()
    df["n_i"] = df["instance_id"].map(baseline_counts)
    df["m_ij"] = [
        scenario_counts.loc[(instance_id, baseline_id)]
        for instance_id, baseline_id in zip(df["instance_id"], df["baseline_id"])
    ]

    df["peso_crashing"] = 1.0 / (
        n_instances * df["n_i"] * df["m_ij"]
    )

    return df


# ============================================================
# Gráfico 1: CVaR95 relativo por gamma
# ============================================================

def generate_gamma_ridgeline(consolidated_dir, output_dir):
    input_file = consolidated_dir / "sensibilidade.csv"

    if not input_file.exists():
        print(f"[AVISO] Arquivo não encontrado: {input_file}")
        return

    df = pd.read_csv(input_file)

    required = {
        "instance_id",
        "baseline_id",
        "gamma",
        "relative_delay_cvar95",
    }

    missing = required.difference(df.columns)

    if missing:
        raise ValueError(
            f"Colunas ausentes em sensibilidade.csv: {sorted(missing)}"
        )

    df = df.dropna(
        subset=[
            "instance_id",
            "baseline_id",
            "gamma",
            "relative_delay_cvar95",
        ]
    ).copy()

    df = add_baseline_weights(df)

    df["relative_delay_cvar95_percent"] = (
        df["relative_delay_cvar95"] * 100.0
    )

    gamma_values = sorted(df["gamma"].unique())

    order = [
        f"γ = {gamma:.2f}".replace(".", ",")
        for gamma in gamma_values
    ]

    groups = {}

    for gamma, label in zip(gamma_values, order):
        groups[label] = df[df["gamma"] == gamma].copy()

    plot_ridgeline(
        groups=groups,
        values_col="relative_delay_cvar95_percent",
        weights_col="peso_baseline",
        order=order,
        title="Distribuição do CVaR95 relativo segundo a intensidade da perturbação",
        xlabel="CVaR95 relativo do atraso (%)",
        output_stem=output_dir / "01_CVaR95_relativo_por_gamma",
        value_formatter=lambda x: f"{x:.7f}%",
        zero_line=False,
        note=(
            "Nota: densidades normalizadas. As linhas tracejadas indicam as medianas ponderadas. "
            "Cada instância recebe peso total igual."
        ),
    )


# ============================================================
# Gráfico 2: FRRI por ganho de Makespan
# ============================================================

def generate_frri_by_makespan_gain(consolidated_dir, output_dir):
    input_file = consolidated_dir / "todos_crashing.csv"

    if not input_file.exists():
        print(f"[AVISO] Arquivo não encontrado: {input_file}")
        return

    usecols = [
        "instance_id",
        "baseline_id",
        "crashing_scenario_id",
        "delta_makespan",
        "frri",
    ]

    df = pd.read_csv(input_file, usecols=usecols)
    df = df.dropna().copy()

    df = add_crashing_weights(df)

    # No arquivo, delta_makespan = makespan_crashing - makespan_original.
    # Portanto, ganho positivo de prazo é -delta_makespan.
    df["ganho_makespan"] = -df["delta_makespan"]

    def classify_gain(value):
        if value <= 0:
            return "Sem redução nominal"
        if value == 1:
            return "Redução de 1 período"
        if value == 2:
            return "Redução de 2 períodos"
        return "Redução de 3 ou mais períodos"

    df["faixa_ganho"] = df["ganho_makespan"].apply(classify_gain)

    order = [
        "Sem redução nominal",
        "Redução de 1 período",
        "Redução de 2 períodos",
        "Redução de 3 ou mais períodos",
    ]

    groups = {
        group: df[df["faixa_ganho"] == group].copy()
        for group in order
    }

    plot_ridgeline(
        groups=groups,
        values_col="frri",
        weights_col="peso_crashing",
        order=order,
        title="Distribuição do FRRI segundo o ganho nominal de prazo",
        xlabel="FRRI",
        output_stem=output_dir / "02_FRRI_por_ganho_de_Makespan",
        value_formatter=lambda x: f"{x:.7f}",
        zero_line=True,
        note=(
            "Nota: densidades normalizadas. As linhas tracejadas indicam as medianas. "
            "A ponderação atribui peso total igual por instância, baseline e cenário."
        ),
    )


# ============================================================
# Gráfico 3: Delta CVaR95 relativo por quartis do SIF
# ============================================================

def generate_delta_cvar_by_sif_quartile(consolidated_dir, output_dir):
    input_file = consolidated_dir / "todos_crashing.csv"

    if not input_file.exists():
        print(f"[AVISO] Arquivo não encontrado: {input_file}")
        return

    usecols = [
        "instance_id",
        "baseline_id",
        "crashing_scenario_id",
        "sif_original",
        "relative_cvar95_original",
        "relative_cvar95_crashing",
    ]

    df = pd.read_csv(input_file, usecols=usecols)
    df = df.dropna().copy()

    df = add_crashing_weights(df)

    df["delta_cvar95_rel"] = (
        df["relative_cvar95_crashing"]
        - df["relative_cvar95_original"]
    )

    baseline_level = (
        df[["instance_id", "baseline_id", "sif_original"]]
        .drop_duplicates()
        .copy()
    )

    n_instances = baseline_level["instance_id"].nunique()

    baseline_counts = (
        baseline_level
        .groupby("instance_id")["baseline_id"]
        .nunique()
    )

    baseline_level["peso_baseline"] = 1.0 / (
        n_instances * baseline_level["instance_id"].map(baseline_counts)
    )

    q1 = weighted_quantile(
        baseline_level["sif_original"],
        baseline_level["peso_baseline"],
        0.25,
    )

    q2 = weighted_quantile(
        baseline_level["sif_original"],
        baseline_level["peso_baseline"],
        0.50,
    )

    q3 = weighted_quantile(
        baseline_level["sif_original"],
        baseline_level["peso_baseline"],
        0.75,
    )

    def classify_sif(value):
        if value <= q1:
            return "Q1 do SIF"
        if value <= q2:
            return "Q2 do SIF"
        if value <= q3:
            return "Q3 do SIF"
        return "Q4 do SIF"

    baseline_level["quartil_sif"] = baseline_level["sif_original"].apply(
        classify_sif
    )

    quartile_map = baseline_level.set_index(
        ["instance_id", "baseline_id"]
    )["quartil_sif"].to_dict()

    df["quartil_sif"] = [
        quartile_map[(instance_id, baseline_id)]
        for instance_id, baseline_id in zip(
            df["instance_id"],
            df["baseline_id"],
        )
    ]

    order = [
        "Q1 do SIF",
        "Q2 do SIF",
        "Q3 do SIF",
        "Q4 do SIF",
    ]

    groups = {
        group: df[df["quartil_sif"] == group].copy()
        for group in order
    }

    plot_ridgeline(
        groups=groups,
        values_col="delta_cvar95_rel",
        weights_col="peso_crashing",
        order=order,
        title="Variação do CVaR95 relativo segundo os quartis do SIF",
        xlabel="ΔCVaR95 relativo",
        output_stem=output_dir / "03_Delta_CVaR95_rel_por_quartis_de_SIF",
        value_formatter=lambda x: f"{x:+.7f}",
        zero_line=True,
        note=(
            "Nota: densidades normalizadas. As linhas tracejadas indicam as medianas. "
            "A ponderação atribui peso total igual por instância, baseline e cenário."
        ),
    )


# ============================================================
# Gráfico 4: instâncias representativas
# ============================================================

def generate_representative_instances(consolidated_dir, output_dir):
    input_file = consolidated_dir / "todos_crashing.csv"

    if not input_file.exists():
        print(f"[AVISO] Arquivo não encontrado: {input_file}")
        return

    usecols = [
        "instance_id",
        "baseline_id",
        "crashing_scenario_id",
        "delta_makespan",
        "sif_original",
        "frri",
    ]

    df = pd.read_csv(input_file, usecols=usecols)
    df = df.dropna().copy()

    df["ganho_makespan"] = -df["delta_makespan"]

    def classify_gain(value):
        if value <= 0:
            return "Sem redução nominal"
        if value == 1:
            return "Redução de 1 período"
        if value == 2:
            return "Redução de 2 períodos"
        return "Redução de 3 ou mais períodos"

    df["faixa_ganho"] = df["ganho_makespan"].apply(classify_gain)

    instance_summary = (
        df.groupby("instance_id")
        .agg(
            cenarios=("crashing_scenario_id", "size"),
            baselines_com_crashing=("baseline_id", "nunique"),
            sif_mediano=("sif_original", "median"),
            sif_medio=("sif_original", "mean"),
        )
        .reset_index()
    )

    eligible = instance_summary[
        instance_summary["cenarios"] >= instance_summary["cenarios"].median()
    ].copy()

    eligible = eligible.sort_values("sif_mediano").reset_index(drop=True)

    target_probs = [0.05, 0.25, 0.45, 0.55, 0.75, 0.95]

    selected = []
    used = set()

    for probability in target_probs:
        target_index = int(round((len(eligible) - 1) * probability))
        selected_instance = None

        for radius in range(len(eligible)):
            candidate_indices = [target_index - radius, target_index + radius]

            for index in candidate_indices:
                if 0 <= index < len(eligible):
                    instance_id = eligible.loc[index, "instance_id"]

                    if instance_id not in used:
                        selected_instance = instance_id
                        used.add(instance_id)
                        break

            if selected_instance is not None:
                break

        if selected_instance is not None:
            selected.append(selected_instance)

    order = [
        "Sem redução nominal",
        "Redução de 1 período",
        "Redução de 2 períodos",
        "Redução de 3 ou mais períodos",
    ]

    selected_rows = []

    for position, instance_id in enumerate(selected, start=1):
        sub = df[df["instance_id"] == instance_id].copy()

        baseline_count = sub["baseline_id"].nunique()
        scenario_counts = sub.groupby("baseline_id").size()

        sub["peso_instancia"] = [
            1.0 / (baseline_count * scenario_counts.loc[baseline_id])
            for baseline_id in sub["baseline_id"]
        ]

        groups = {
            group: sub[sub["faixa_ganho"] == group].copy()
            for group in order
        }

        output_stem = output_dir / (
            f"04_{position:02d}_{instance_id}_FRRI_por_ganho"
        )

        plot_ridgeline(
            groups=groups,
            values_col="frri",
            weights_col="peso_instancia",
            order=order,
            title=(
                f"Distribuição do FRRI por ganho nominal de prazo\n"
                f"Instância {instance_id}"
            ),
            xlabel="FRRI",
            output_stem=output_stem,
            value_formatter=lambda x: f"{x:.7f}",
            zero_line=True,
            note=(
                "Nota: densidades normalizadas. As linhas tracejadas indicam as medianas. "
                "A ponderação reparte o peso da instância entre seus baselines e cenários."
            ),
        )

        selected_rows.append(
            {
                "ordem": position,
                "instance_id": instance_id,
                "cenarios": int(sub.shape[0]),
                "baselines_com_crashing": int(sub["baseline_id"].nunique()),
                "sif_mediano": float(sub["sif_original"].median()),
                "sif_medio": float(sub["sif_original"].mean()),
            }
        )

    selected_table = pd.DataFrame(selected_rows)
    selected_table.to_csv(
        output_dir / "04_instancias_representativas_selecionadas.csv",
        index=False,
    )


# ============================================================
# Execução
# ============================================================

def main():
    parser = argparse.ArgumentParser(
        description=(
            "Gera gráficos ridgeline para os resultados do Capítulo 4 "
            "a partir da pasta descompactada Chapter4_Results-FINAL."
        )
    )

    parser.add_argument(
        "--input-dir",
        required=True,
        help=(
            "Caminho para a pasta descompactada Chapter4_Results-FINAL. "
            "Exemplo: C:/dados/Chapter4_Results-FINAL"
        ),
    )

    parser.add_argument(
        "--output-dir",
        default="Ridgeline_Crashing_Estilo_Tese",
        help="Pasta de saída dos gráficos.",
    )

    parser.add_argument(
        "--zip-output",
        action="store_true",
        help="Compacta os gráficos gerados em um arquivo ZIP.",
    )

    args = parser.parse_args()

    input_dir = Path(args.input_dir)
    output_dir = Path(args.output_dir)

    consolidated_dir = input_dir / "03_consolidated"

    if not consolidated_dir.exists():
        raise FileNotFoundError(
            f"Pasta não encontrada: {consolidated_dir}"
        )

    output_dir.mkdir(parents=True, exist_ok=True)

    print("[1/4] Gerando CVaR95 relativo por gamma...")
    generate_gamma_ridgeline(consolidated_dir, output_dir)

    print("[2/4] Gerando FRRI por ganho de Makespan...")
    generate_frri_by_makespan_gain(consolidated_dir, output_dir)

    print("[3/4] Gerando ΔCVaR95 relativo por quartis do SIF...")
    generate_delta_cvar_by_sif_quartile(consolidated_dir, output_dir)

    print("[4/4] Gerando instâncias representativas...")
    generate_representative_instances(consolidated_dir, output_dir)

    if args.zip_output:
        zip_path = output_dir.with_suffix(".zip")

        with zipfile.ZipFile(
            zip_path,
            "w",
            compression=zipfile.ZIP_DEFLATED,
        ) as zf:
            for file in sorted(output_dir.iterdir()):
                if file.is_file():
                    zf.write(file, arcname=file.name)

        print(f"ZIP gerado: {zip_path}")

    print(f"Concluído. Arquivos salvos em: {output_dir}")


if __name__ == "__main__":
    main()