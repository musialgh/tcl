# =====================================================================
# Skating System (TSO Anhang 2) — expressed in the DSL
# =====================================================================

SYSTEM "SkatingSystem" {

  # ---- Definitions (computed columns over judge scores) -------------
  DEFINE count_le(P)  AS COUNT  judges WHERE place <= P
  DEFINE sum_le(P)    AS SUM    place OF judges WHERE place <= P
  DEFINE majority     AS floor(judge_count / 2) + 1

  # ---- Per-dance evaluation ----------------------------------------
  EVALUATE_DANCE {

    FOR rank R FROM 1 TO couple_count {

      # Rule 5: find couples with absolute majority for "R or better"
      LET candidates =
        SELECT couples
        WHERE  count_le(R) >= majority
        AND    not_yet_placed

      WHEN candidates IS_EMPTY:
        CONTINUE                                  # try next R column

      # Rule 6: largest majority wins
      LET groups = GROUP candidates BY count_le(R) ORDER BY count_le(R) DESC

      FOR group IN groups {

        WHEN group.size == 1:
          ASSIGN group[0] => next_free_rank
          REASON "Rule 5/6: majority {count_le(R)} at <= {R}"

        WHEN group.size > 1:
          # Rule 7a: tiebreak by sum of qualifying places (lower = better)
          LET sub = GROUP group BY sum_le(R) ORDER BY sum_le(R) ASC

          FOR g IN sub {
            WHEN g.size == 1:
              ASSIGN g[0] => next_free_rank
              REASON "Rule 7a: sum {sum_le(R)} at <= {R}"

            WHEN g.size > 1:
              # Rule 7b: extend to next place column
              EXTEND_COLUMN UNTIL resolved OR R == couple_count

              # Rule 7c + 7d: still tied → share averaged rank
              WHEN still_tied:
                ASSIGN g => SHARED average(next_free_ranks(g.size))
                REASON "Rule 7c/7d: shared rank"
          }
      }
    }
  }
}


EVALUATE_TOURNAMENT {

  # Rule 8: total = sum of dance places per couple
  FOR couple IN couples {
    LET place_sum = SUM dance_place OF dances
  }

  # Rule 9: lowest place_sum wins
  RANK couples BY place_sum ASC

  # Rule 9b/10: tiebreak by "R or better" then sum
  ON_TIE {
    FOR R FROM 1 TO couple_count {
      LET cnt = COUNT dance_place OF dances WHERE dance_place <= R
      RANK_TIED BY cnt DESC
      WHEN still_tied:
        LET s = SUM dance_place OF dances WHERE dance_place <= R
        RANK_TIED BY s ASC
    }
  }

  # Rule 11: still tied → treat all dances as one big dance
  ON_TIE {
    APPLY EVALUATE_DANCE TREATING all_dance_places AS single_dance
  }
}