// Signature templates (to be matched by all adjucation schemes) =======================================================


/// Evaluate a part of a round (e.g. a single dance) of a single participant
template_evaluate_part_of_round: 
    marks   : { adjudicators_group+ : { adjudicator_short+ : num } }, ///< marks by adjudicator "initials", optionally structured in groups
    params  : { param* : num }
->
    report? : { colum-tree+ : any },  ///< data for result reports, optionally structured in groups
    score   : { index+      : num };  ///< sequence of numbers, higher is better, for left-to-right comparison between  participants


/// Define how to aggregate place ranges (e.g. "1st and 2nd place") into a place value when comparing participants
template_aggregate_place_range: 
    pmin : num,
    pmax : num 
-> 
    place : num;


/// Evaluate a round of a single participant based on single part evaluation results
template_evaluate_round: 
    marks   : { part+ : { adjudicators_group+ : { adjudicator_short+ : num } } }, 
    params  : { param* : num }
->
    report? : { colum-tree+ : any },  ///< data for result reports, optionally structured in groups
    score   : { index+      : num };  ///< sequence of numbers, higher is better, for left-to-right comparison between  participants


/// Refine round evaluation to resolve ties (or any cases too complex for template_evaluate_round())
template_tie_round: 
    marks   : { part+ : { adjudicators_group+ : { adjudicator_short+ : num } } }, 
    params  : { param* : num }
->
    report? : { colum-tree+ : any },  ///< data for result reports, optionally structured in groups
    score   : { index+      : num };  ///< sequence of numbers, higher is better, for left-to-right comparison between  participants


// Import all adjudication libraries ===================================================================================

import cross-majority-skating as cms;
// import equality_classification as eq_cls;
// ...