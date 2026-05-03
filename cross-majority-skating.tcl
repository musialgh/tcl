// Implementations specific to cross-majority-skating scheme ===========================================================



/// Evaluate a single dance in an elimination round
evaluate_dance_elimination(template_evaluate_element): 
    /// marks are bool (0/1, 1=TRUE iff crossed), jury is not partitioned
    marks   : { start_no+ : { "jury" : { adjudicator_short+ : bool } } }
->
    /// report number of crosses per participant
    report  : { start_no+ : { "count" : nat } },            
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } };

evaluate_dance_elimination(marks) = 
    // below, no value expression given in count() => count non-zero values
    // -- i.e. equivalent to count(marks in my_marks["jury"], my_marks)
    report = map(my_marks in marks, { "count" : count(my_marks["jury"]) }),
    // negative report values can be used to obtain placing (higher count is better)
    // -- basic scalar arithmetics, such as prefix minus, are defined element-wise as a language feature
    places = compute_places(-report);



/// Evaluate a single dance in a final round, using majority rules, no skating yet
evaluate_dance_final(template_evaluate_element): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } 
->
    /// report, per place except the last, number and sum of same-or-better placings
    report  : { start_no+ : { place+ : { "count" : nat, "sum" : nat } } },            
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } };

evaluate_dance_final(marks) = 
    report = map(
        my_marks in marks, 
        concat(
            place in 1..num_starts,
            { place : count_and_sum(place, my_marks["jury"]) }
        )
    ),
    places = compute_places(score)
where
    score = map(
        my_no, my_marks in marks, 
        concat(
            place in 1..num_starts,
            // ordering by number of majority votes, then sum (core of majority judging)
            // count is negated in score sequence, since higher is better there (and 0es will not be affected)
            { 2*place-1 : -report[my_no][place]["count"], 2*place : +report[my_no][place]["sum"] }
        )
    );



/// Evaluate an elimination round
evaluate_round_elimination(template_evaluate_round):
    /// marks are bool (0/1, 1=TRUE iff crossed), jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : bool } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// report number of crosses per participant
    report  : { start_no+ : { "count" : nat } },            
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } };

evaluate_round_elimination(marks, places) = 
    report = map(
        my_no in field,
        { "count" : sum(dance in marks, count(dance[my_no]["jury"]) }
    ),
    // more crosses is better => must negate report values
    //  -- basic scalar arithmetics, such as prefix minus, are defined element-wise as a language feature
    places = compute_places(-report)
where
    field = keys(first(places)); // returns object of all key : key mappings (start numbers)



/// Evaluate a full final round with majority rules, no skating yet
evaluate_round_final(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// report sum of place values
    report  : { start_no+ : { "places-sum" : real } },            
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } };

evaluate_round_final(marks, places) = 
    report = map(
        my_no in field, 
        { "places-sum" : sum(dance in places, dance[my_no]["pval"]) }
    ),
    // perform ordering by sum (lower is better)
    places = compute_places(report)
where
    field = keys(first(places));



/// Evaluate a full final round with skating
evaluate_round_final_skating(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// report sum of place values
    report  : { start_no+ : { "places-sum" : real } },            
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } };

evaluate_round_final_skating(marks, places) = 
    report = merge(majority_report, skating_report), // recursive merge does what we want
    places = compute_places(scores_up)               // final scoring delivers final placing
where
    // number of places in list: all except last possible
    num = count(first(places));
    // invoke majority system as a basis
    majority_report, majority_places = evaluate_round_final(marks, places),
    // initialize score sequences with single place value obtained from majority evaluation
    scores_down = map(start in majority_places, { 0 : start["pval"] });
    // invoke recursive refinement from here
    scores_up   = skating_refine_r10(marks, places, scores_down);
    // extract full skating report from obtained skating scores (please note negation where higher is better)
    skating_report = map(
        my_score in scores_up, merge(
            index, value in my_score[1..], 
            { "R10" : { index                   : { "count" : -value } } }  if index <= num        |
            { "R11" : { ceil((index - num) / 2) : { "count" : -value } } }  if (index-num) % 2 = 1 |
            { "R11" : { ceil((index - num) / 2) : { "sum"   :  value } } }
        )
    )



/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R10 for <for_place>,
/// but then recursively invoke either R10 on the next place, or R11 on the same.
skating_refine_r10:
    marks       : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    places      : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } },
    /// score list as far as already constructed 
    scores      : { start_no+ : { 0 : real, place* : { "R10"? : nat, "R11"? : nat } } },
    /// target place to be resolved next (default value is for start of recursion)
    for_place   : nat = 1
->
    scores      : { start_no+ : object };

skating_refine_r10(marks, places, scores, for_place) =
    scores                                                       if for_place > #field |
    skating_refine_r10(marks, places,     scores, for_place + 1) if #candidates < 2    |
    skating_refine_r11(marks, places, r10_scores, for_place)
where 
    field           = keys(first(places)),
    r10_places      = compute_places(scores),
    candidates      = keys(filter(range in r10_places, range["pmin"] = for_place)),
    // please note that everything below is only computed if <r10_scores> is required above for recursion
    // compute R10 score values solely for candidate starts
    r10_values      = map(start in candidates, -count(dance in places, dance[start]["pval"] <= for_place)),
    // determine best value of competing starts
    r10_chosen      = arg_min(r10_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new R10 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r10_column      = map(
        start in candidates, 
        { for_place : { "R10" : for_place               } } if ?r10_chosen[start] | 
        { for_place : { "R10" : for_place + #r10_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r10_scores = merge(scores, r10_column);

        
/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R11 for <for_place>,
/// but then recursively invoke R10 on the next place
skating_refine_r11:
    marks       : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    places      : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } },
    /// score list as far as already constructed 
    scores      : { start_no+ : object },
    /// target place to be resolved next
    for_place   : nat
->
    scores      : { start_no+ : object };

skating_refine_r11(marks, places, scores, for_place) =
    scores                                                       if for_place > #field |
    skating_refine_r11(marks, places,     scores, for_place + 1) if #candidates < 2    |
    skating_refine_r11(marks, places, r11_scores, for_place)
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
                jkey, my_mark in my_marks,
                { { jkey, dkey } : my_mark } // objects are sortable and may therefore serve as keys
            )
        )    
    ),
    // compute counts and sums for candidatestarts as for single dances, but only from <for_dance> onwards
    r11_rows        = map(
        start in candidates,
        map(
            place in for_place..#field, // map() works since range sets key = value pairs
            count_and_sum(place, r11_marks)    
        )    
    ),
    // negate counts (higher is better) and use safely-ordered keys -- not relying on "count" < "sum" ;-)
    r11_values      = map(start in r11_rows, map(place in start, { 1 : -place["count"], 2 : +place["sum"] })),
    // determine best value of competing starts
    r11_chosen      = arg_min(r11_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new r11 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r11_column      = map(
        start in candidates, 
        { for_place : { "R11" : for_place               } } if ?r11_chosen[start] | 
        { for_place : { "R11" : for_place + #r11_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r11_scores = merge(scores, r11_column);



// Helpers =============================================================================================================



/// Convert a list of score strings into a list of place ranges.
/// ATT: We assume *lower* score value is better, as in marks or place or time values!
compute_places: 
    scores : { start_no+ : object } 
->
    places : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } };

compute_places(scores) =
    map(
        score in scores, 
        { "pmin": pmin, "pmax": pmax, "pval" : pval }
        where 
            // please note: element-wise comparison of objects, in key order, is a language feature
            pmin = 1       + count(other_score in scores, other_score < score) + 1,
            pmax = #scores - count(other_score in scores, other_score > score),
            pval = (pmin + pmax) / 2
    );



/// Calculate count and sum of "x and better" places, clipped to (0,0) if majority is not reached
count_and_sum: place : nat, marks : { adjudicator_short+ : nat } -> count : nat, sum : nat;

count_and_sum(place, marks) =
    count =  #relevant       if is_majority | 0,
    sum   =  sum(relevant)   if is_majority | 0
where
    relevant    = filter(mark in marks, mark <= place),
    is_majority = #relevant > (#marks / 2);


