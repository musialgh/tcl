// Signature templates (to be matched by all adjucation schemes) =======================================================


/// Evaluate a full round, potentially consisting of multiple elements (e.g. dances)
template_evaluate_round: 
    /// marks by adjudicator "initials", optionally structured in groups, 
    /// for each participant, then for each element of the round (e.g. dance)
    marks : { start_no+ : { round_element+ : { adjudicators_group+ : { adjudicator_short+ : num } } } }
->
    /// data for result reports, using up to three grouping levels between <round_element> and <key>
    /// - unused grouping levels (including <round_element>) to use "" as the respective key
    /// - two values are obligatory per start (actual placing):
    ///   - start_no : { "" : { ... { "place_min" : { value : nat, reason : txt } } ... } }
    ///   - start_no : { "" : { ... { "place_max" : { value : nat, reason : txt } } ... } }
    report : { start_no+ : { round_element+ : { group+ : 
                                                { subgroup+ : 
                                                  { subsubgroup+ : 
                                                    { key+ : { "value" : num, "reason" : txt } } } } } } };