                       lorom                                ;      |        |      ;  
                       ORG $808000                          ;      |        |      ;  
          CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;  
               Fn_go1: STA.W $0100,X                        ;808003|9D0001  |800100; Store some stuff
               Test22: DEX                                  ;808006|CA      |      ; LineComment
                       BPL CODE_808000                      ;808007|10F7    |808000;  
                       Test_Data = $80805B                  ;      |        |      ;  
