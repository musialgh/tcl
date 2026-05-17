// ===============================================
// Implementation of cross-majority-skating scheme 
// ===============================================


import "tas-evaluation-interface";


evaluate_round_elimination(round):
evaluate_round_final(round, do_skating = FALSE):
perform_skating(unplaced, tie, starts, jury, round, dances, final_marks, for_place):


/// Evaluate elimination round (crosses)
evaluate_round_elimination(template_evaluate_round); // use template signature unchanged

evaluate_round_elimination(marks) = 
    report = merge(dance_counts, total_counts, placings)
where
    // compute number of crosses per dance
    dance_counts = map(
        my_marks in marks, map(
            dance in my_marks, 
            { "" : { "" : { "" : { "marks" : { 
                "value"  : sum(dance), // recursive sum of values
                "reason" : "F 6.8.1: count of marks"
            } } } } } 
        )
    ),
    // compute total number of crosses
    total_counts = map(
        my_marks in marks,
        { "" : { "" : { "" : { "" : { "marks" : { 
            "value"  : sum(my_marks), // recursive sum of values
            "reason" : "F 6.8.1: count of marks"
        } } } } } }
    ),
    // generate placings
    placings = map(
        my_marks in total_counts, 
        { "" : { "" : { "" : { "" : { 
            "place_min" : { "value"  : pmin, "reason" : reason },
            "place_max" : { "value"  : pmax, "reason" : reason }
        } } } } }
    where
        // re-read accumulated data from report -- not beautiful, but prevents redundant calculation
        my_value = my_marks[""][""][""][""]["marks"]["value"],
        pmin = 1             + count(peer in total_counts, peer[""][""][""][""]["marks"]["value"] > my_value),
        pmax = #total_counts - count(peer in total_counts, peer[""][""][""][""]["marks"]["value"] < my_value),    
        reason = "F 6.8.1: single rank {pmin} in count of marks"           if pmin = pmax
               | "F 6.8.1: shared rank {pmin}..{pmax} in count of marks"
    );
// evaluate_round_elimination()


/// Evaluate one dance in a final
evaluate_dance_final:
    /// *all* starts participating in the dance, key = value
    field     : { start_no+ : nat },
    /// marks of *remaining* (unplaced) starts only
    marks(template_evaluate_round), ///< use same-name argument pattern from template
    dance     : any,
    for_place : nat = 1,
->
    report(template_evaluate_round)

evaluate_dance_final(field, marks, dance, for_place) =
    // terminate when all starts have been placed
    report_up                                                                       if #unplaced = 0 |
    // continue with next free place for all remaining starts
    merge(report_up, eval_dance(field, unplaced, dance, for_place + #marks - #unplaced))
where
    // invoke "tie" refinement for the current place, which will return a report
    report_up = refine_r5(field, marks, dance, for_place),
    // determine starts still unplaced, by removing start(s) just placed by above call
    unplaced = filter(start, _ in marks, not ?report_up[start][dance][""][""][""]["place_min"]);
// evaluate_dance_final()


/// Recursively refine a tie for <for_place>, starting with R5 from <by_place>
refine_r5:
    field     : { start_no+ : nat },
    marks(template_evaluate_round),
    dance       : any,
    for_place   : nat,
    by_place    : nat = 1
->
    report(template_evaluate_round);

refine_r5(field, marks, dance, for_place, by_place) =
    merge(report_counts, single)                                                if #candidates = 1 |
    merge(report_counts, refine_r7(field, unplaced, dance, for_place, by_place)) 
where
    // extract relevant count column
    r5_count = map(my_marks in marks, count(mark in my_marks[dance][""], mark <= by_place)),
    // suppress sub-majority results (note: TSO also reports sub-majority counts -- but we don't like that!)
    r5_count_m = filter(count in r5_count, 2 * count > #jury),
    // report counts considered
    report_counts = map(
        my_count in r5_count_m,
        { dance : { by_place : { "" : { "" : { "count": { 
            "value"  : my_count, 
            "reason" : "Rules 5,6 for place {by_place}"
        } } } } } }
    ),    
    candidates = arg_max(r5_count_m), // returns key -> key mapping(s) for all occurrences of the maximum
    unplaced   = filter(start, _ in marks, ?candidates[start]),
    single     = assign_place(
        first(candidates), dance, for_place, for_place, 
        "Rules 5,6,8: highest majority count {max(r5_count_m)} on place {by_place}"
    );
// refine_r5()    


/// Recursively refine a tie for <for_place>, ustarting with R7 from <by_place>
refine_r7(refine_r5); // using refiner5 signature unchanged => no need to re-define

refine_r7(field, marks, dance, for_place, by_place) =
    merge(report_sums, single)                                                  if #candidates = 1   |
    merge(report_sums, refine_r5(field, marks, dance, for_place, by_place + 1)) if by_place < #field |
    merge(report_sums, shared)
where
    // extract relevant sum column
    r5_count = map(my_marks in marks, count(mark in my_marks[dance][""],         mark <= by_place)),
    r7_sum   = map(my_marks in marks, sum  (mark in my_marks[dance][""], mark if mark <= by_place | 0)),
    // report sums considered
    report_sums = map(
        my_sum in r7_sum,
        { dance : { by_place : { "" : { "" : { "sum": { 
            "value"  : my_sum, 
            "reason" : "Rule 7 for place {by_place}"
        } } } } } }
    ),    
    candidates = arg_min(r7_sum), // returns key -> key mapping(s) for all occurrences of the minimum
    unplaced   = filter(start, _ in marks, ?candidates[start]),
    single     = assign_place(
        first(candidates), dance, for_place, for_place, 
        "Rules 7a,b: majority count {max(r5_count)} with lowest sum {min(r7_sum)} on place {by_place}"
    ),
    shared = {}
// refine_r7()    






/// Evaluate a full final round with majority rules, no skating yet
evaluate_round_final(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// place ranges use natural numbers, place value may be fractional (average)
    places_out  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } },
    /// report sum of place values
    report_out  : { start_no+ : { "places-sum" : real } };  

evaluate_round_final(marks, places) = 
    report_out = map(
        my_no in field, 
        { "places-sum" : sum(dance in places, dance[my_no]["pval"]) }
    ),
    // perform ordering by sum (lower is better)
    places_out = compute_places(report_out)
where
    field = keys(first(places));



/// Evaluate a full final round with skating
evaluate_round_final_skating(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// place ranges use natural numbers, place value may be fractional (average)
    places_out : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } },
    /// full report including skating stuff
    report_out :  { start_no+ : { "places-sum" : real, 
                                  place*       : { "R10" : nat, "R11"? : nat },
                                  "R10"?       : { place+ : { "count" : nat, "sum" : real } },
                                  "R11"?       : { place+ : { "count" : nat, "sum" : nat  } }
                  } };

evaluate_round_final_skating(marks, places) = 
    report_out = report_up,
    places_out = compute_places(scores_up) // final scoring delivers final placing
where
    // number of places in list: all except last possible
    num = count(first(places));
    // invoke majority system as a basis
    majority_places, majority_report = evaluate_round_final(marks, places),
    // initialize scores with single place value obtained from majority evaluation
    scores_down = map(start in majority_places, { 0 : start["pval"] });
    // invoke recursive refinement from here
    report_up, scores_up = skating_refine_r10(marks, places, majority_report, scores_down);



/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R10 for <for_place>,
/// but then recursively invoke either R10 on the next place, or R11 on the same.
skating_refine_r10:
    marks       : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    places      : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } },
    /// structured table of report values per start (see return for format)
    report      : { start_no+ : object },
    /// score list as far as already constructed  (see return for format)
    scores      : { start_no+ : object },
    /// target place to be resolved next (default value is for start of recursion)
    for_place   : nat = 1
->
    report_out  : { start_no+ : { "places-sum" : real, 
                                  place*       : { "R10" : nat, "R11"? : nat },
                                  "R10"?       : { place+ : { "count" : nat, "sum" : real } },
                                  "R11"?       : { place+ : { "count" : nat, "sum" : nat  } }
                  } },
    scores_out  : { start_no+ : { 0      : real,                            // places-sum
                                  place* : { "R10" : nat, "R11"? : nat }    // places assigned by skating
                  } };

skating_refine_r10(marks, places, report, scores, for_place) =
    { "report_out" : report, "scores_out" : scores }                            if for_place > #field |
    skating_refine_r10(marks, places,     report,     scores, for_place + 1)    if #candidates < 2    |
    skating_refine_r11(marks, places, r10_report, r10_scores, for_place    )
where 
    field           = keys(first(places)),
    r10_places      = compute_places(scores),
    candidates      = keys(filter(range in r10_places, range["pmin"] = for_place)),
    // please note that everything below is only computed if <r10_scores> is required above for recursion
    // compute counts and sums for same-or-better placings over whole dances (*no* majority clipping!)
    r10_data        = map(
        start in candidates,
        { "rule10" : { for_place : count_and_sum(for_place, map(dance in places, dance[start]["pval"])) } }    
    ),
    // compute R10 score values (negating counts, as higher count is better)
    r10_values      = map(
        start in r10_data, 
        { 1 : -start["rule10"][for_place]["count"], 2 : +start["rule10"][for_place]["sum"] }
    ),
    // determine best value of competing starts
    r10_chosen      = arg_min(r10_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new R10 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r10_placing     = map(
        start in candidates, 
        { for_place : { "R10" : for_place               } } if ?r10_chosen[start] | 
        { for_place : { "R10" : for_place + #r10_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r10_scores      = merge(scores, r10_placing),
    r10_report      = merge(report, r10_placing, r10_data);

        
/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R11 for <for_place>,
/// but then recursively invoke R10 on the next place
skating_refine_r11(skating_refine_r10); // using r10 signature unchanged => no need to re-define

skating_refine_r11(marks, places, report, scores, for_place) =
    { "report_out" : report, "scores_out" : scores }                            if for_place > #field |
    skating_refine_r10(marks, places,     report,     scores, for_place + 1)    if #candidates < 2    |
    skating_refine_r10(marks, places, r11_report, r11_scores, for_place + 1)
where 
    field           = keys(first(places)),
    r11_places      = compute_places(scores),
    candidates      = keys(filter(range in r11_places, range["pmin"] = for_place)),
    // please note that everything below is only computed if <r11_scores> is required above for recursion
    // flatten marks for all dances, per start, under a "merged key" for adjudicators and dances
    r11_marks       = merge(
        dkey, dance in marks,
        map(
            my_marks in dance,
            merge(
                jkey, my_mark in my_marks["jury"],
                { { 1 : jkey, 2 : dkey } : my_mark } // objects are sortable and may therefore serve as keys
            )
        )    
    ),
    // compute counts and sums for candidate starts as for single dances, but only from <for_dance> onwards
    r11_data        = map(
        start in candidates,
        { 
            "rule11" : map(
                place in for_place..#field, // map() works since range sets key = value pairs
                count_and_sum_maj(place, r11_marks[start])
            )
        }
    ),
    // negate counts (higher is better) and use safely-ordered keys -- not relying on "count" < "sum" ;-)
    r11_values      = map(start in r11_data, map(place in start["rule11"], { 1 : -place["count"], 2 : +place["sum"] })),
    // determine best value of competing starts
    r11_chosen      = arg_min(r11_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new r11 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r11_placing     = map(
        start in candidates, 
        { for_place : { "R11" : for_place               } } if ?r11_chosen[start] | 
        { for_place : { "R11" : for_place + #r11_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r11_scores      = merge(scores, r11_placing),
    // as <r11_data> may overlap with earlier calls, we need to use replace() instead of merge()
    r11_report      = replace(merge(report, r11_placing), r11_data);



// Helpers =============================================================================================================



/// Convert a list of score strings into a list of place ranges.
/// ATT: We assume *lower* score value is better, as in marks or place or time values!
compute_places: 
    scores : { start_no+ : object } 
->
    places : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } };

compute_places(scores) =
    map(
        score in scores, 
        { "pmin": pmin, "pmax": pmax, "pval" : pval }
        where 
            // please note: element-wise comparison of objects, in key order, is a language feature
            pmin = 1       + count(other_score in scores, other_score < score),
            pmax = #scores - count(other_score in scores, other_score > score),
            pval = (pmin + pmax) / 2
    );



/// Calculate count and sum of "x and better" places
count_and_sum: 
    place : nat, marks : { origin+ : real } -> count : nat, sum : nat;

count_and_sum(place, marks) =
    count =  #relevant,   
    sum   =  sum(relevant)
where
    relevant = filter(mark in marks, mark <= place);



/// Calculate count and sum of "x and better" places, (0,0) if majority is not reached
count_and_sum_maj(count_and_sum); // re-use signature

count_and_sum_maj(place, marks) =
    data if data["count"] > (#marks / 2) | { "count" : 0, "sum" : 0 }
where
    data = count_and_sum(place, marks);
