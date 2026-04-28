// Implementations specific to cross-majority-skating scheme ===========================================================


/// Evaluate elimination dance with cross markings (expressed as bool, i.e. 0/1)
evaluate_dance_elimination(template_evaluate_part_of_round):
    marks   : { "jury" : { adjudicator_short+ : bool } },
    params  : { ignore* : any }
->  
    report  : { "count" : nat },  
    score   : { "count" : nat };

evaluate_dance_elimination(marks, params) =
    report: count(marks["jury"]), // use values from range if no value expression given
    score:  report;


/// Evaluate final dance with majority rule (no skating here)
evaluate_dance_final(template_evaluate_part_of_round):
    marks   : { "jury" : { adjudicator_short+ : nat } },
    params  : { "num_couples" : nat, "num_adjudicators" : nat }
->
    report  : { place+ : { "count" : nat, "sum" : nat } },  
    score   : { index+ : nat };

evaluate_dance_final(marks, params) = 
    report, // for labelled outputs, this is short for: "report" : report
    score
where
    report = object(place in 1..params["num_couples"]-1, key: place, value: count_and_sum(place, marks["jury"])),
    score  = concat(place in 1..params["num_couples"]-1, { 2*place-1 : +rep[place]["count"], 2*place : -rep[place]["sum"] });


/// Calculate count and sum of "x and better" places, clipped to (0,0) if majority is not reached
count_and_sum: nat, { adjudicator_short+ : nat } -> count : nat, sum : nat;

count_and_sum(place, marks) =
    count:  len(relevant)   if is_majority | 0,
    sum:    sum(relevant)   if is_majority | 0
where
    relevant    = filter(mark in marks, mark <= place),
    is_majority = len(relevant) > len(marks) / 2;


/// Evaluate final round (no skating here) 
evaluate_round_final(template_evaluate_round):
    marks   : { part+ : { "jury" : { adjudicator_short+ : nat } } }, 
    params  : { "num_couples" : nat, "num_adjudicators" : nat }
->
    report? : { colum-tree+ : any },  ///< data for result reports, optionally structured in groups
    score   : { index+      : nat };  ///< sequence of numbers, higher is better, for left-to-right comparison between  participants


/// Evaluate final round with skating (if needed)
evaluate_round_final_skating()